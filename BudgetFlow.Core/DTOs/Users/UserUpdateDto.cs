using System.ComponentModel.DataAnnotations;

namespace BudgetFlow.Core.DTOs;

// Request: no password here - updating details never touches the password
public class UserUpdateDto
{
    [Required, EmailAddress, StringLength(100)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    [Required, Phone, StringLength(20)]
    public string Phone { get; set; } = string.Empty;
}
