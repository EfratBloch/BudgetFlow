using BudgetFlow.Core.Enums;

namespace BudgetFlow.Core.Entities;

// Money the coordinator added to one pot of one guide.
// A guide pot's budget = its formula + the sum of the additions to it.
// A coordinator pot's budget = its formula - the sum of the transfers out of it.
// Deleting an addition is how it is cancelled - there is no stored balance to fix.
public class BudgetAddition
{
    public int Id { get; set; }

    public int GuideId { get; set; }
    public User Guide { get; set; } = null!;

    // Activity or Birthday - the guide pot that receives the money
    public BudgetSource TargetPot { get; set; }

    public decimal Amount { get; set; } // always > 0
    public string Reason { get; set; } = string.Empty;

    public BudgetAdditionKind Kind { get; set; }

    // GuideMeetings or Annual when Kind is Transfer; null when Kind is Extra
    public BudgetSource? FromPot { get; set; }

    public DateTime CreatedAt { get; set; } // UTC
}
