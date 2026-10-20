using BudgetFlow.Core.Enums;

namespace BudgetFlow.Core.DTOs;

// Response: never contains PasswordHash
public class UserDto
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public DateTime CreatedAt { get; set; }
}
