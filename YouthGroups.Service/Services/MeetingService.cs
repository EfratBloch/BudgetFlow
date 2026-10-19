using AutoMapper;
using YouthGroups.Core.Common;
using YouthGroups.Core.DTOs;
using YouthGroups.Core.Entities;
using YouthGroups.Core.Enums;
using YouthGroups.Core.Interfaces.Repositories;
using YouthGroups.Core.Interfaces.Services;
using YouthGroups.Service.Budget;
using YouthGroups.Service.Common;

namespace YouthGroups.Service.Services;

// Reporting meetings. Create and update run the same checks, in this fixed order:
//   1. date in the future                               -> Validation
//   2. meeting type that belongs to the other role      -> Forbidden
//   3. a participant who is not the reporter's          -> Forbidden
//   4. expense total against the remaining amount in the pot of the meeting type
//      - guide, Activity meeting, over budget, activity + birthday together can cover it:
//          AllowBirthdayDraw = false -> Conflict with shortfall and birthdayAvailable
//          AllowBirthdayDraw = true  -> SPLIT: what is left in the activity pot is charged
//                                       to Activity, only the shortfall to Birthday
//      - over budget in any other case                  -> Conflict, final
// The split deliberately differs from the PLAN ("charged in full to one pot, no split"):
// the coordinator asked that the activity pot is used up first and neither pot goes negative.
// The meeting, its expenses and its attendance are saved in ONE transaction.
public class MeetingService : IMeetingService
{
    private const string MeetingNotFound = "Meeting not found.";
    private const string OwnerOnly = "Only the user who reported this meeting may change it.";

    private readonly IMeetingRepository _meetings;
    private readonly IParticipantRepository _participants;
    private readonly IUnitOfWork _unitOfWork;
    private readonly BudgetPots _pots;
    private readonly IMapper _mapper;

    public MeetingService(
        IMeetingRepository meetings,
        IParticipantRepository participants,
        IUnitOfWork unitOfWork,
        BudgetPots pots,
        IMapper mapper)
    {
        _meetings = meetings;
        _participants = participants;
        _unitOfWork = unitOfWork;
        _pots = pots;
        _mapper = mapper;
    }

    public async Task<Result<PagedResult<MeetingListItemDto>>> GetPagedAsync(
        MeetingQueryDto query, CurrentUser caller, CancellationToken ct)
    {
        if (!caller.IsCoordinator && query.UserId.HasValue)
            return Result<PagedResult<MeetingListItemDto>>.Forbidden("A guide may only list her own meetings.");

        // A guide always gets her own meetings - the filter is forced, not trusted
        var filter = new MeetingQueryDto
        {
            UserId = caller.IsCoordinator ? query.UserId : caller.UserId,
            Type = query.Type,
            From = query.From,
            To = query.To,
            Sort = query.Sort,
            Page = query.Page,
            PageSize = query.PageSize
        };

        return Result<PagedResult<MeetingListItemDto>>.Ok(await _meetings.GetPagedAsync(filter, ct));
    }

    public async Task<Result<MeetingDto>> GetByIdAsync(int id, CurrentUser caller, CancellationToken ct)
    {
        var meeting = await _meetings.GetWithDetailsAsync(id, ct);
        if (meeting is null)
            return Result<MeetingDto>.NotFound(MeetingNotFound);

        if (!caller.IsCoordinator && meeting.UserId != caller.UserId)
            return Result<MeetingDto>.Forbidden("A guide may only view her own meetings.");

        return Result<MeetingDto>.Ok(_mapper.Map<MeetingDto>(meeting));
    }

    public async Task<Result<MeetingDto>> CreateAsync(MeetingCreateDto dto, CurrentUser caller, CancellationToken ct)
    {
        var type = dto.Type!.Value; // [Required] on the DTO

        // The budget is read and the meeting is written in the same transaction
        await using var transaction = await _unitOfWork.BeginTransactionAsync(ct);

        var check = await CheckAsync(type, dto.Date!.Value, dto.ParticipantIds, dto.Expenses,
            dto.AllowBirthdayDraw, caller, excludeMeetingId: null, ct);
        if (check.Error is not null)
            return Fail<MeetingDto>(check.Error);

        var meeting = new Meeting
        {
            UserId = caller.UserId, // from the JWT, never from the body
            Type = type,
            Date = check.Date,
            Program = dto.Program.Trim(),
            Notes = dto.Notes?.Trim()
        };

        foreach (var participant in check.Participants)
            meeting.Participants.Add(participant);

        foreach (var expense in NewExpenses(dto.Expenses, check))
            meeting.Expenses.Add(expense);

        await _meetings.AddAsync(meeting, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        // Reload with the reporter's name and the saved ids
        var saved = await _meetings.GetWithDetailsAsync(meeting.Id, ct);
        return Result<MeetingDto>.Ok(_mapper.Map<MeetingDto>(saved!));
    }

    public async Task<Result> UpdateAsync(int id, MeetingUpdateDto dto, CurrentUser caller, CancellationToken ct)
    {
        await using var transaction = await _unitOfWork.BeginTransactionAsync(ct);

        // Tracked, with Expenses and Participants, so both collections can be replaced
        var meeting = await _meetings.GetWithDetailsForUpdateAsync(id, ct);
        if (meeting is null)
            return Result.NotFound(MeetingNotFound);

        if (meeting.UserId != caller.UserId)
            return Result.Forbidden(OwnerOnly);

        // The type is not editable. excludeMeetingId: the meeting's old expenses
        // are about to be replaced, so they must not count against the pot.
        var check = await CheckAsync(meeting.Type, dto.Date!.Value, dto.ParticipantIds, dto.Expenses,
            dto.AllowBirthdayDraw, caller, excludeMeetingId: id, ct);
        if (check.Error is not null)
            return check.Error;

        meeting.Date = check.Date;
        meeting.Program = dto.Program.Trim();
        meeting.Notes = dto.Notes?.Trim();

        // Expenses are replaced as a whole: removed ones are orphans, which EF deletes
        meeting.Expenses.Clear();
        foreach (var expense in NewExpenses(dto.Expenses, check))
            meeting.Expenses.Add(expense);

        meeting.Participants.Clear();
        foreach (var participant in check.Participants)
            meeting.Participants.Add(participant);

        await _unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> DeleteAsync(int id, CurrentUser caller, CancellationToken ct)
    {
        var meeting = await _meetings.GetForUpdateAsync(id, ct);
        if (meeting is null)
            return Result.NotFound(MeetingNotFound);

        if (meeting.UserId != caller.UserId)
            return Result.Forbidden(OwnerOnly);

        // Expenses and attendance rows go with it (cascade). There is no balance
        // to repair: balances are computed from the remaining expenses.
        _meetings.Remove(meeting);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Ok();
    }

    // The four checks shared by create and update, in the required order.
    // On success returns the UTC date, the tracked participants and the pot to charge.
    private async Task<CheckResult> CheckAsync(
        MeetingType type,
        DateTime date,
        IReadOnlyCollection<int> participantIds,
        IReadOnlyCollection<ExpenseCreateDto> expenses,
        bool allowBirthdayDraw,
        CurrentUser caller,
        int? excludeMeetingId,
        CancellationToken ct)
    {
        // 1. Date
        var utcDate = Dates.AsUtc(date);
        if (utcDate > DateTime.UtcNow)
            return CheckResult.Fail(Result.Validation("A meeting cannot be reported for a future date."));

        // 2. Type against role
        var isCoordinatorType = type is MeetingType.GuidesMeeting or MeetingType.SpecialEvent;
        if (isCoordinatorType && !caller.IsCoordinator)
            return CheckResult.Fail(Result.Forbidden("Only the coordinator may report a guides meeting or a special event."));
        if (!isCoordinatorType && caller.IsCoordinator)
            return CheckResult.Fail(Result.Forbidden("Activity and birthday meetings are reported by guides."));

        // 3. Participants
        var ids = participantIds.Distinct().ToList();
        var participants = await _participants.GetByIdsForUpdateAsync(ids, ct);

        if (participants.Count != ids.Count)
            return CheckResult.Fail(Result.Validation("One or more participants do not exist."));

        if (!caller.IsCoordinator && participants.Any(p => p.GuideId != caller.UserId))
            return CheckResult.Fail(Result.Forbidden("A guide may only report her own participants."));

        // 4. Budget
        var source = SourceFor(type);
        var total = expenses.Sum(e => e.Amount);

        if (total > 0)
        {
            // The same pot numbers the budget screen shows, including the coordinator's additions
            var pot = await _pots.GetAsync(source, caller.UserId, excludeMeetingId, ct);

            // Remaining can be negative if the rates were lowered after spending
            var available = Math.Max(pot.Remaining, 0m);

            if (total > available)
            {
                var shortfall = total - available;

                if (type != MeetingType.Activity)
                    return OverBudget(pot, shortfall);

                var birthday = await _pots.GetAsync(BudgetSource.Birthday, caller.UserId, excludeMeetingId, ct);
                var birthdayAvailable = Math.Max(birthday.Remaining, 0m);

                // Even both pots together are not enough
                if (shortfall > birthdayAvailable)
                    return OverBudget(pot, shortfall);

                if (!allowBirthdayDraw)
                    return CheckResult.Fail(Result.Conflict(
                        "There is not enough left in the activity budget. "
                        + "Confirm to charge the missing amount to the birthday budget.",
                        new Dictionary<string, object>
                        {
                            ["shortfall"] = shortfall,
                            ["birthdayAvailable"] = birthdayAvailable
                        }));

                // Split: the first `available` goes to Activity, the shortfall to Birthday
                return new CheckResult(null, utcDate, participants, source, ActivityLimit: available);
            }
        }

        return new CheckResult(null, utcDate, participants, source);
    }

    // One expense row per request expense, charged to the meeting's pot. With a split,
    // expenses fill the activity pot in order; the expense that crosses the limit becomes
    // two rows with the same description - one Activity, one Birthday - and the rest go to Birthday.
    private static List<Expense> NewExpenses(IEnumerable<ExpenseCreateDto> dtos, CheckResult check)
    {
        var result = new List<Expense>();
        var activityLeft = check.ActivityLimit;

        foreach (var dto in dtos)
        {
            var description = dto.Description.Trim();

            if (activityLeft is null)
            {
                result.Add(NewExpense(dto.Amount, description, check.Source));
                continue;
            }

            var toActivity = Math.Min(dto.Amount, activityLeft.Value);
            var toBirthday = dto.Amount - toActivity;
            activityLeft -= toActivity;

            if (toActivity > 0)
                result.Add(NewExpense(toActivity, description, BudgetSource.Activity));
            if (toBirthday > 0)
                result.Add(NewExpense(toBirthday, description, BudgetSource.Birthday));
        }

        return result;
    }

    private static CheckResult OverBudget(BudgetPotDto pot, decimal shortfall)
        => CheckResult.Fail(Result.Conflict(
            $"This meeting is over the {pot.Label.ToLowerInvariant()} budget.",
            new Dictionary<string, object>
            {
                ["shortfall"] = shortfall,
                ["remaining"] = pot.Remaining
            }));

    // The meeting type decides the pot - the reporter never picks it
    private static BudgetSource SourceFor(MeetingType type) => type switch
    {
        MeetingType.Activity => BudgetSource.Activity,
        MeetingType.Birthday => BudgetSource.Birthday,
        MeetingType.GuidesMeeting => BudgetSource.GuideMeetings,
        MeetingType.SpecialEvent => BudgetSource.Annual,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };

    private static Expense NewExpense(decimal amount, string description, BudgetSource source) => new()
    {
        Amount = amount,
        Description = description,
        BudgetSource = source
    };

    // Copies a failed non-generic Result into a Result<T> with the same status and details
    private static Result<T> Fail<T>(Result error) => error.Status switch
    {
        ResultStatus.Validation => Result<T>.Validation(error.Error!),
        ResultStatus.Forbidden => Result<T>.Forbidden(error.Error!),
        ResultStatus.NotFound => Result<T>.NotFound(error.Error!),
        _ => Result<T>.Conflict(error.Error!, error.Details)
    };

    // ActivityLimit is set only for a split: the amount charged to Activity before Birthday
    private sealed record CheckResult(
        Result? Error,
        DateTime Date,
        IReadOnlyList<Participant> Participants,
        BudgetSource Source,
        decimal? ActivityLimit = null)
    {
        public static CheckResult Fail(Result error)
            => new(error, default, Array.Empty<Participant>(), default);
    }
}
