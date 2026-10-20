using AutoMapper;
using Microsoft.Extensions.Logging;
using BudgetFlow.Core.Common;
using BudgetFlow.Core.DTOs;
using BudgetFlow.Core.Entities;
using BudgetFlow.Core.Enums;
using BudgetFlow.Core.Interfaces.Repositories;
using BudgetFlow.Core.Interfaces.Services;
using BudgetFlow.Service.Budget;
using BudgetFlow.Service.Common;

namespace BudgetFlow.Service.Services;

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
    private readonly IEquipmentRepository _equipment;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly BudgetPots _pots;
    private readonly IMapper _mapper;
    private readonly ILogger<MeetingService> _logger;

    public MeetingService(
        IMeetingRepository meetings,
        IParticipantRepository participants,
        IEquipmentRepository equipment,
        IUserRepository users,
        IUnitOfWork unitOfWork,
        BudgetPots pots,
        IMapper mapper,
        ILogger<MeetingService> logger)
    {
        _meetings = meetings;
        _participants = participants;
        _equipment = equipment;
        _users = users;
        _unitOfWork = unitOfWork;
        _pots = pots;
        _mapper = mapper;
        _logger = logger;
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
            GuidesOnly = caller.IsCoordinator && query.GuidesOnly,
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

        var equipmentError = await CheckEquipmentAsync(type, dto.EquipmentId, ct);
        if (equipmentError is not null)
            return Fail<MeetingDto>(equipmentError);

        var guides = await CheckGuidesAsync(type, dto.ParticipantIds, dto.GuideIds, ct);
        if (guides.Error is not null)
            return Fail<MeetingDto>(guides.Error);

        var meeting = new Meeting
        {
            UserId = caller.UserId, // from the JWT, never from the body
            Type = type,
            Date = check.Date,
            Program = dto.Program.Trim(),
            Notes = dto.Notes?.Trim(),
            EquipmentId = dto.EquipmentId
        };

        foreach (var participant in check.Participants)
            meeting.Participants.Add(participant);

        foreach (var guide in guides.Guides)
            meeting.Guides.Add(guide);

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

        var equipmentError = await CheckEquipmentAsync(meeting.Type, dto.EquipmentId, ct);
        if (equipmentError is not null)
            return equipmentError;

        var guides = await CheckGuidesAsync(meeting.Type, dto.ParticipantIds, dto.GuideIds, ct);
        if (guides.Error is not null)
            return guides.Error;

        meeting.Date = check.Date;
        meeting.Program = dto.Program.Trim();
        meeting.Notes = dto.Notes?.Trim();
        meeting.EquipmentId = dto.EquipmentId;

        // Expenses are replaced as a whole: removed ones are orphans, which EF deletes
        meeting.Expenses.Clear();
        foreach (var expense in NewExpenses(dto.Expenses, check))
            meeting.Expenses.Add(expense);

        meeting.Participants.Clear();
        foreach (var participant in check.Participants)
            meeting.Participants.Add(participant);

        meeting.Guides.Clear();
        foreach (var guide in guides.Guides)
            meeting.Guides.Add(guide);

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
                    return OverBudget(pot, shortfall, type, caller);

                var birthday = await _pots.GetAsync(BudgetSource.Birthday, caller.UserId, excludeMeetingId, ct);
                var birthdayAvailable = Math.Max(birthday.Remaining, 0m);

                // Even both pots together are not enough
                if (shortfall > birthdayAvailable)
                    return OverBudget(pot, shortfall, type, caller);

                if (!allowBirthdayDraw)
                {
                    _logger.LogWarning(
                        "Budget overrun: {Type} meeting by user {UserId} is {Shortfall} over the activity budget; "
                        + "asking to confirm a draw from the birthday budget ({BirthdayAvailable} available)",
                        type, caller.UserId, shortfall, birthdayAvailable);

                    return CheckResult.Fail(Result.Conflict(
                        "There is not enough left in the activity budget. "
                        + "Confirm to charge the missing amount to the birthday budget.",
                        new Dictionary<string, object>
                        {
                            ["shortfall"] = shortfall,
                            ["birthdayAvailable"] = birthdayAvailable
                        }));
                }

                // Split: the first `available` goes to Activity, the shortfall to Birthday.
                // Not a warning - the guide confirmed it, so this is the normal flow.
                _logger.LogInformation(
                    "Budget split: {Shortfall} of the {Type} meeting by user {UserId} is charged to the birthday budget",
                    shortfall, type, caller.UserId);
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

    // The store item is only a record of what was used: it must exist and belong to an
    // Activity meeting, but its status and holder are not checked or changed
    private async Task<Result?> CheckEquipmentAsync(MeetingType type, int? equipmentId, CancellationToken ct)
    {
        if (equipmentId is null)
            return null;

        if (type != MeetingType.Activity)
            return Result.Validation("A store item can only be recorded on an activity meeting.");

        if (await _equipment.GetByIdAsync(equipmentId.Value, ct) is null)
            return Result.Validation("The store item does not exist.");

        return null;
    }

    // A guides meeting is attended by guides, not participants; every other type is attended
    // by participants only. Returns the tracked guides to attach to the meeting.
    private async Task<(Result? Error, IReadOnlyList<User> Guides)> CheckGuidesAsync(
        MeetingType type, IReadOnlyCollection<int> participantIds, IReadOnlyCollection<int>? guideIds, CancellationToken ct)
    {
        var ids = (guideIds ?? Array.Empty<int>()).Distinct().ToList();

        if (type != MeetingType.GuidesMeeting)
            return ids.Count == 0
                ? (null, Array.Empty<User>())
                : (Result.Validation("Only a guides meeting is attended by guides."), Array.Empty<User>());

        if (participantIds.Count > 0)
            return (Result.Validation("A guides meeting is attended by guides, not participants."), Array.Empty<User>());

        var guides = await _users.GetGuidesByIdsForUpdateAsync(ids, ct);
        if (guides.Count != ids.Count)
            return (Result.Validation("One or more guides do not exist."), Array.Empty<User>());

        return (null, guides);
    }

    // A refused meeting is a Warning in the log: handled (the caller got a 409), but worth seeing
    private CheckResult OverBudget(BudgetPotDto pot, decimal shortfall, MeetingType type, CurrentUser caller)
    {
        _logger.LogWarning(
            "Budget overrun: {Type} meeting by user {UserId} refused, {Shortfall} over the {Pot} budget (remaining {Remaining})",
            type, caller.UserId, shortfall, pot.Label, pot.Remaining);

        return CheckResult.Fail(Result.Conflict(
            $"This meeting is over the {pot.Label.ToLowerInvariant()} budget.",
            new Dictionary<string, object>
            {
                ["shortfall"] = shortfall,
                ["remaining"] = pot.Remaining
            }));
    }

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
