using System.Globalization;
using Microsoft.Extensions.Options;
using YouthGroups.Core.DTOs;
using YouthGroups.Core.Enums;
using YouthGroups.Core.Settings;

namespace YouthGroups.Service.Budget;

// The budget formulas, in one place. Pure arithmetic over the appsettings constants
// and counts that the caller already loaded - no database access here.
// BudgetService uses it to show the pots; MeetingService uses it to check a new expense.
public class BudgetCalculator
{
    private readonly BudgetSettings _settings;

    public BudgetCalculator(IOptions<BudgetSettings> options)
    {
        _settings = options.Value;
    }

    // Coordinator - guide meetings pot: PerGuideMeeting * guideCount * GuideMeetingsPerYear
    public BudgetPotDto GuideMeetingsPot(int guideCount, decimal spent) => Pot(
        BudgetSource.GuideMeetings,
        "Guide meetings",
        $"{Money(_settings.PerGuideMeeting)} x {guideCount} guides x {_settings.GuideMeetingsPerYear} meetings",
        _settings.PerGuideMeeting * guideCount * _settings.GuideMeetingsPerYear,
        spent);

    // Coordinator - annual pot: PerParticipantAnnual * totalParticipants
    //   + SpecialMeetingsPerYear * PerParticipantSpecialMeeting * totalParticipants
    public BudgetPotDto AnnualPot(int totalParticipants, decimal spent) => Pot(
        BudgetSource.Annual,
        "Annual",
        $"{Money(_settings.PerParticipantAnnual)} x {totalParticipants} participants"
            + $" + {_settings.SpecialMeetingsPerYear} special meetings x {Money(_settings.PerParticipantSpecialMeeting)} x {totalParticipants} participants",
        _settings.PerParticipantAnnual * totalParticipants
            + _settings.SpecialMeetingsPerYear * _settings.PerParticipantSpecialMeeting * totalParticipants,
        spent);

    // Guide - activity pot: a flat amount
    public BudgetPotDto ActivityPot(decimal spent) => Pot(
        BudgetSource.Activity,
        "Activities",
        $"{Money(_settings.GuideActivityAnnual)} (fixed)",
        _settings.GuideActivityAnnual,
        spent);

    // Guide - birthday pot: PerParticipantBirthday * her participant count
    public BudgetPotDto BirthdayPot(int participantCount, decimal spent) => Pot(
        BudgetSource.Birthday,
        "Birthdays",
        $"{Money(_settings.PerParticipantBirthday)} x {participantCount} participants",
        _settings.PerParticipantBirthday * participantCount,
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

    // "12.5", not "12.50" or "12,5" - the formula text is the same on every machine
    private static string Money(decimal amount) => amount.ToString("0.##", CultureInfo.InvariantCulture);
}
