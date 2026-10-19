using YouthGroups.Core.Enums;

namespace YouthGroups.Core.DTOs;

// Response
public class BudgetAdditionDto
{
    public int Id { get; set; }
    public int GuideId { get; set; }
    public string GuideName { get; set; } = string.Empty;
    public BudgetSource TargetPot { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public BudgetAdditionKind Kind { get; set; }
    public BudgetSource? FromPot { get; set; }
    public DateTime CreatedAt { get; set; }
}
