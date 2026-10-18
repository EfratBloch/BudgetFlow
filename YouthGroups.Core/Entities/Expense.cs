using YouthGroups.Core.Enums;

namespace YouthGroups.Core.Entities;

public class Expense
{
    public int Id { get; set; }

    public int MeetingId { get; set; }
    public Meeting Meeting { get; set; } = null!;

    public decimal Amount { get; set; } // always > 0
    public string Description { get; set; } = string.Empty;

    // Which pot paid for it. Usually follows the meeting type, except an Activity
    // meeting the guide explicitly agreed to charge to her birthday pot.
    public BudgetSource BudgetSource { get; set; }
}
