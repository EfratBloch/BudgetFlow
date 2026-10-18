using System.ComponentModel.DataAnnotations;

namespace YouthGroups.Core.DTOs;

// Request: the coordinator adds a guide and chooses her initial password
public class UserCreateDto
{
    [Required, EmailAddress, StringLength(100)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 8)]
    public string InitialPassword { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    [Required, Phone, StringLength(20)]
    public string Phone { get; set; } = string.Empty;

    [Required, RegularExpression(@"^\d{9}$", ErrorMessage = "National ID must be exactly 9 digits.")]
    public string NationalId { get; set; } = string.Empty;
}
