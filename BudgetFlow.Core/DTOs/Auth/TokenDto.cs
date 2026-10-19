using BudgetFlow.Core.Enums;

namespace BudgetFlow.Core.DTOs;

// Response: returned after a successful login
public class TokenDto
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; } // UTC
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public UserRole Role { get; set; }
}
