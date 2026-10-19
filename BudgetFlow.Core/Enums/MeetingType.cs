namespace BudgetFlow.Core.Enums;

// The meeting type decides which budget pot pays for it - the reporter never picks the pot.
public enum MeetingType
{
    Activity,       // guide's activity pot
    Birthday,       // guide's birthday pot
    GuidesMeeting,  // coordinator's guide-meetings pot
    SpecialEvent    // coordinator's annual pot
}
