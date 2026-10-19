using Microsoft.Extensions.Options;
using BudgetFlow.Core.Settings;
using BudgetFlow.Service.Budget;

namespace BudgetFlow.Tests;

// The same rates as the "Budget" section of appsettings.json, so the expected numbers
// in the tests are the ones the coordinator really sees.
internal static class TestBudget
{
    public static BudgetSettings Settings => new()
    {
        PerGuideMeeting = 20,
        GuideMeetingsPerYear = 4,
        PerParticipantAnnual = 35,
        SpecialMeetingsPerYear = 2,
        PerParticipantSpecialMeeting = 5,
        GuideActivityAnnual = 300,
        PerParticipantBirthday = 4
    };

    public static BudgetCalculator Calculator => new(Options.Create(Settings));
}
