using System.Globalization;
using Microsoft.Extensions.Options;
using BudgetFlow.Core.DTOs;
using BudgetFlow.Core.Enums;
using BudgetFlow.Core.Settings;

namespace BudgetFlow.Service.Budget;

// The budget formulas, in one place. Pure arithmetic over the appsettings constants
// and the counts and sums that the caller already loaded - no database access here.
// Guide pots:       formula + the coordinator's additions to that pot.
// Coordinator pots: formula - the transfers out of that pot to guides.
public class BudgetCalculator
{
    private readonly BudgetSettings _settings;

    public BudgetCalculator(IOptions<BudgetSettings> options)
    {
        _settings = options.Value;
    }

    // Coordinator - guide meetings pot: PerGuideMeeting * guideCount * GuideMeetingsPerYear
    public BudgetPotDto GuideMeetingsPot(int guideCount, decimal spent, decimal transferred) => Pot(
        BudgetSource.GuideMeetings,
        "Guide meetings",
        $"{Money(_settings.PerGuideMeeting)} x {guideCount} guides x {_settings.GuideMeetingsPerYear} meetings"
            + Transferred(transferred),
        _settings.PerGuideMeeting * guideCount * _settings.GuideMeetingsPerYear - transferred,
        spent);

    // Coordinator - annual pot: PerParticipantAnnual * totalParticipants
    //   + SpecialMeetingsPerYear * PerParticipantSpecialMeeting * totalParticipants
    public BudgetPotDto AnnualPot(int totalParticipants, decimal spent, decimal transferred) => Pot(
        BudgetSource.Annual,
        "Annual",
        $"{Money(_settings.PerParticipantAnnual)} x {totalParticipants} participants"
            + $" + {_settings.SpecialMeetingsPerYear} special meetings x {Money(_settings.PerParticipantSpecialMeeting)} x {totalParticipants} participants"
            + Transferred(transferred),
        _settings.PerParticipantAnnual * totalParticipants
            + _settings.SpecialMeetingsPerYear * _settings.PerParticipantSpecialMeeting * totalParticipants
            - transferred,
        spent);

    // Guide - activity pot: a flat amount
    public BudgetPotDto ActivityPot(decimal spent, decimal added) => Pot(
        BudgetSource.Activity,
        "Activities",
        $"{Money(_settings.GuideActivityAnnual)} (fixed)" + Added(added),
        _settings.GuideActivityAnnual + added,
        spent);

    // Guide - birthday pot: PerParticipantBirthday * her participant count
    public BudgetPotDto BirthdayPot(int participantCount, decimal spent, decimal added) => Pot(
        BudgetSource.Birthday,
        "Birthdays",
        $"{Money(_settings.PerParticipantBirthday)} x {participantCount} participants" + Added(added),
        _settings.PerParticipantBirthday * participantCount + added,
        spent);

    private static BudgetPotDto Pot(BudgetSource source, string label, string formula, decimal budget, decimal spent)
        => new()
        {
            Source = source,
            Label = label,
            Formula = formula,
            Budget = budget,
            Spent = spent,
            Remaining = budget - spent // may be negative if the rates were lowered after spending
        };

    private static string Added(decimal added) => added > 0 ? $" + {Money(added)} added" : string.Empty;

    private static string Transferred(decimal transferred)
        => transferred > 0 ? $" - {Money(transferred)} transferred" : string.Empty;

    // "12.5", not "12.50" or "12,5" - the formula text is the same on every machine
    private static string Money(decimal amount) => amount.ToString("0.##", CultureInfo.InvariantCulture);
}
