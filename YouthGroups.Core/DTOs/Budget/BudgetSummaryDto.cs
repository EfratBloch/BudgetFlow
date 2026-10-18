namespace YouthGroups.Core.DTOs;

// Response: GET /api/budget/summary - the coordinator and every guide in one call
public class BudgetSummaryDto
{
    public UserBudgetDto Coordinator { get; set; } = new();
    public List<UserBudgetDto> Guides { get; set; } = new();
}
