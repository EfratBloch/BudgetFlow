namespace YouthGroups.Core.Settings;

// Bound to the "Budget" section of appsettings.json through IOptions<BudgetSettings>.
// Changing a rate means editing the file, not the code.
public class BudgetSettings
{
    public const string SectionName = "Budget";

    // Coordinator - guide meetings pot: PerGuideMeeting * guideCount * GuideMeetingsPerYear
    public decimal PerGuideMeeting { get; set; }
    public int GuideMeetingsPerYear { get; set; }

    // Coordinator - annual pot: PerParticipantAnnual * totalParticipants
    //   + SpecialMeetingsPerYear * PerParticipantSpecialMeeting * totalParticipants
    public decimal PerParticipantAnnual { get; set; }
    public int SpecialMeetingsPerYear { get; set; }
    public decimal PerParticipantSpecialMeeting { get; set; }

    // Guide - activity pot: a flat amount
    public decimal GuideActivityAnnual { get; set; }

    // Guide - birthday pot: PerParticipantBirthday * her participant count
    public decimal PerParticipantBirthday { get; set; }
}
