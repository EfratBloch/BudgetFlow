using YouthGroups.Core.Enums;

namespace YouthGroups.Core.DTOs;

// Response
public class ExpenseDto
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public BudgetSource BudgetSource { get; set; }
}
