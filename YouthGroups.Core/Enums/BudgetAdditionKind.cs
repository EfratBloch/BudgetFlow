namespace YouthGroups.Core.Enums;

// Where the money of a budget addition comes from - the coordinator picks one each time.
public enum BudgetAdditionKind
{
    Extra,    // new money: the coordinator's pots do not change
    Transfer  // moved from one of the coordinator's pots (GuideMeetings or Annual)
}
