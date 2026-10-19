using YouthGroups.Core.DTOs;
using YouthGroups.Core.Enums;
using YouthGroups.Core.Interfaces.Repositories;

namespace YouthGroups.Service.Budget;

// Loads what one pot needs (counts, spending, additions or transfers) and hands it to
// BudgetCalculator. BudgetService, MeetingService and BudgetAdditionService all read a pot
// through here, so a pot has the same budget on the budget screen and in every check.
public class BudgetPots
{
    private readonly IUserRepository _users;
    private readonly IParticipantRepository _participants;
    private readonly IMeetingRepository _meetings;
    private readonly IBudgetAdditionRepository _additions;
    private readonly BudgetCalculator _calculator;

    public BudgetPots(
        IUserRepository users,
        IParticipantRepository participants,
        IMeetingRepository meetings,
        IBudgetAdditionRepository additions,
        BudgetCalculator calculator)
    {
        _users = users;
        _participants = participants;
        _meetings = meetings;
        _additions = additions;
        _calculator = calculator;
    }

    // userId is the pot's owner: the guide for Activity / Birthday, the coordinator for
    // GuideMeetings / Annual. excludeMeetingId leaves out a meeting that is being updated.
    public async Task<BudgetPotDto> GetAsync(
        BudgetSource source, int userId, int? excludeMeetingId, CancellationToken ct)
    {
        var spentBySource = await _meetings.GetSpentBySourceAsync(userId, excludeMeetingId, ct);
        var spent = spentBySource.TryGetValue(source, out var amount) ? amount : 0m;

        return source switch
        {
            BudgetSource.Activity => _calculator.ActivityPot(
                spent,
                await _additions.SumAddedAsync(userId, BudgetSource.Activity, ct)),

            BudgetSource.Birthday => _calculator.BirthdayPot(
                await _participants.CountByGuideAsync(userId, ct),
                spent,
                await _additions.SumAddedAsync(userId, BudgetSource.Birthday, ct)),

            BudgetSource.GuideMeetings => _calculator.GuideMeetingsPot(
                await _users.CountGuidesAsync(ct),
                spent,
                await _additions.SumTransferredFromAsync(BudgetSource.GuideMeetings, ct)),

            BudgetSource.Annual => _calculator.AnnualPot(
                await _participants.CountAllAsync(ct),
                spent,
                await _additions.SumTransferredFromAsync(BudgetSource.Annual, ct)),

            _ => throw new ArgumentOutOfRangeException(nameof(source), source, null)
        };
    }
}
