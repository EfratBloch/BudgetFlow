using BudgetFlow.Core.Enums;

namespace BudgetFlow.Core.DTOs;

// Response: GET /api/users/{id}/budget - the two pots of one user
public class UserBudgetDto
{
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public List<BudgetPotDto> Pots { get; set; } = new();
}
