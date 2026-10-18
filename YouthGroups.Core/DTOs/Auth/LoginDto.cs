using System.ComponentModel.DataAnnotations;

namespace YouthGroups.Core.DTOs;

// Request
public class LoginDto
{
    [Required, EmailAddress, StringLength(100)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string Password { get; set; } = string.Empty;
}
