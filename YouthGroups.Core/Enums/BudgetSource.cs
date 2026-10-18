namespace YouthGroups.Core.Enums;

// The pot an expense was charged to. Stored on every expense so that remaining
// balances can be computed with a single SUM grouped by this column.
public enum BudgetSource
{
    Activity,
    Birthday,
    GuideMeetings,
    Annual
}
