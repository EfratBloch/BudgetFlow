using BudgetFlow.Core.Common;
using BudgetFlow.Core.DTOs;
using BudgetFlow.Core.Entities;
using BudgetFlow.Core.Enums;
using BudgetFlow.Core.Interfaces.Repositories;
using BudgetFlow.Core.Interfaces.Services;
using BudgetFlow.Service.Budget;

namespace BudgetFlow.Service.Services;

// Every budget is computed on each call - nothing is stored, so there is no balance
// that can drift out of date when a meeting is edited or deleted.
// remaining = ceiling - sum of the expenses charged to that pot.
public class BudgetService : IBudgetService
{
    private readonly IUserRepository _users;
    private readonly IMeetingRepository _meetings;
    private readonly IBudgetAdditionRepository _additions;
    private readonly BudgetCalculator _calculator;
    private readonly BudgetPots _pots;

    public BudgetService(
        IUserRepository users,
        IMeetingRepository meetings,
        IBudgetAdditionRepository additions,
        BudgetCalculator calculator,
        BudgetPots pots)
    {
        _users = users;
        _meetings = meetings;
        _additions = additions;
        _calculator = calculator;
        _pots = pots;
    }

    public async Task<Result<UserBudgetDto>> GetUserBudgetAsync(int userId, CurrentUser caller, CancellationToken ct)
    {
        // Checked before the lookup, so a guide cannot learn which ids exist
        if (!caller.IsCoordinator && caller.UserId != userId)
            return Result<UserBudgetDto>.Forbidden("A guide may only view her own budget.");

        var user = await _users.GetByIdAsync(userId, ct);
        if (user is null)
            return Result<UserBudgetDto>.NotFound("User not found.");

        BudgetSource[] sources = user.Role == UserRole.Coordinator
            ? [BudgetSource.GuideMeetings, BudgetSource.Annual]
            : [BudgetSource.Activity, BudgetSource.Birthday];

        var pots = new List<BudgetPotDto>();
        foreach (var source in sources)
            pots.Add(await _pots.GetAsync(source, userId, null, ct));

        return Result<UserBudgetDto>.Ok(ToDto(user, pots));
    }

    public async Task<Result<BudgetSummaryDto>> GetSummaryAsync(CancellationToken ct)
    {
        var coordinator = await _users.GetCoordinatorAsync(ct);
        if (coordinator is null)
            return Result<BudgetSummaryDto>.NotFound("No coordinator is registered yet.");

        // ONE query for all guides: participant counts, spending and additions per pot, computed in SQL
        var rows = await _users.GetGuideBudgetRowsAsync(ct);

        // Every participant belongs to a guide, so the rows already hold both totals
        var guideCount = rows.Count;
        var totalParticipants = rows.Sum(r => r.ParticipantCount);

        var coordinatorSpent = await _meetings.GetSpentBySourceAsync(coordinator.Id, null, ct);

        return Result<BudgetSummaryDto>.Ok(new BudgetSummaryDto
        {
            Coordinator = ToDto(coordinator, new List<BudgetPotDto>
            {
                _calculator.GuideMeetingsPot(
                    guideCount,
                    SpentOn(coordinatorSpent, BudgetSource.GuideMeetings),
                    await _additions.SumTransferredFromAsync(BudgetSource.GuideMeetings, ct)),
                _calculator.AnnualPot(
                    totalParticipants,
                    SpentOn(coordinatorSpent, BudgetSource.Annual),
                    await _additions.SumTransferredFromAsync(BudgetSource.Annual, ct))
            }),
            Guides = rows.Select(r => new UserBudgetDto
            {
                UserId = r.UserId,
                FullName = r.FullName,
                Role = UserRole.Guide,
                Pots = new List<BudgetPotDto>
                {
                    _calculator.ActivityPot(r.ActivitySpent, r.ActivityAdded),
                    _calculator.BirthdayPot(r.ParticipantCount, r.BirthdaySpent, r.BirthdayAdded)
                }
            }).ToList()
        });
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
