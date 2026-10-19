using YouthGroups.Core.Common;
using YouthGroups.Core.DTOs;
using YouthGroups.Core.Entities;
using YouthGroups.Core.Enums;
using YouthGroups.Core.Interfaces.Repositories;
using YouthGroups.Core.Interfaces.Services;
using YouthGroups.Service.Budget;

namespace YouthGroups.Service.Services;

// Every budget is computed on each call - nothing is stored, so there is no balance
// that can drift out of date when a meeting is edited or deleted.
// remaining = ceiling - sum of the expenses charged to that pot.
public class BudgetService : IBudgetService
{
    private readonly IUserRepository _users;
    private readonly IParticipantRepository _participants;
    private readonly IMeetingRepository _meetings;
    private readonly BudgetCalculator _calculator;

    public BudgetService(
        IUserRepository users,
        IParticipantRepository participants,
        IMeetingRepository meetings,
        BudgetCalculator calculator)
    {
        _users = users;
        _participants = participants;
        _meetings = meetings;
        _calculator = calculator;
    }

    public async Task<Result<UserBudgetDto>> GetUserBudgetAsync(int userId, CurrentUser caller, CancellationToken ct)
    {
        // Checked before the lookup, so a guide cannot learn which ids exist
        if (!caller.IsCoordinator && caller.UserId != userId)
            return Result<UserBudgetDto>.Forbidden("A guide may only view her own budget.");

        var user = await _users.GetByIdAsync(userId, ct);
        if (user is null)
            return Result<UserBudgetDto>.NotFound("User not found.");

        // One SUM ... GROUP BY BudgetSource over this user's expenses
        var spent = await _meetings.GetSpentBySourceAsync(userId, null, ct);

        var pots = user.Role == UserRole.Coordinator
            ? await CoordinatorPotsAsync(spent, ct)
            : await GuidePotsAsync(userId, spent, ct);

        return Result<UserBudgetDto>.Ok(ToDto(user, pots));
    }

    public async Task<Result<BudgetSummaryDto>> GetSummaryAsync(CancellationToken ct)
    {
        var coordinator = await _users.GetCoordinatorAsync(ct);
        if (coordinator is null)
            return Result<BudgetSummaryDto>.NotFound("No coordinator is registered yet.");

        // ONE query for all guides: participant counts and spending per pot, computed in SQL
        var rows = await _users.GetGuideBudgetRowsAsync(ct);

        // Every participant belongs to a guide, so the rows already hold both totals
        var guideCount = rows.Count;
        var totalParticipants = rows.Sum(r => r.ParticipantCount);

        var coordinatorSpent = await _meetings.GetSpentBySourceAsync(coordinator.Id, null, ct);

        return Result<BudgetSummaryDto>.Ok(new BudgetSummaryDto
        {
            Coordinator = ToDto(coordinator, new List<BudgetPotDto>
            {
                _calculator.GuideMeetingsPot(guideCount, SpentOn(coordinatorSpent, BudgetSource.GuideMeetings)),
                _calculator.AnnualPot(totalParticipants, SpentOn(coordinatorSpent, BudgetSource.Annual))
            }),
            Guides = rows.Select(r => new UserBudgetDto
            {
                UserId = r.UserId,
                FullName = r.FullName,
                Role = UserRole.Guide,
                Pots = new List<BudgetPotDto>
                {
                    _calculator.ActivityPot(r.ActivitySpent),
                    _calculator.BirthdayPot(r.ParticipantCount, r.BirthdaySpent)
                }
            }).ToList()
        });
    }

    private async Task<List<BudgetPotDto>> CoordinatorPotsAsync(
        IReadOnlyDictionary<BudgetSource, decimal> spent, CancellationToken ct)
    {
        var guideCount = await _users.CountGuidesAsync(ct);
        var totalParticipants = await _participants.CountAllAsync(ct);

        return new List<BudgetPotDto>
        {
            _calculator.GuideMeetingsPot(guideCount, SpentOn(spent, BudgetSource.GuideMeetings)),
            _calculator.AnnualPot(totalParticipants, SpentOn(spent, BudgetSource.Annual))
        };
    }

    private async Task<List<BudgetPotDto>> GuidePotsAsync(
        int guideId, IReadOnlyDictionary<BudgetSource, decimal> spent, CancellationToken ct)
    {
        var participantCount = await _participants.CountByGuideAsync(guideId, ct);

        return new List<BudgetPotDto>
        {
            _calculator.ActivityPot(SpentOn(spent, BudgetSource.Activity)),
            _calculator.BirthdayPot(participantCount, SpentOn(spent, BudgetSource.Birthday))
        };
    }

    // A pot with no expenses yet is simply missing from the GROUP BY result
    private static decimal SpentOn(IReadOnlyDictionary<BudgetSource, decimal> spent, BudgetSource source)
        => spent.TryGetValue(source, out var amount) ? amount : 0m;

    private static UserBudgetDto ToDto(User user, List<BudgetPotDto> pots) => new()
    {
        UserId = user.Id,
        FullName = user.FullName,
        Role = user.Role,
        Pots = pots
    };
}
