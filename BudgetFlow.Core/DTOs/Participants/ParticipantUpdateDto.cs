using System.ComponentModel.DataAnnotations;

namespace BudgetFlow.Core.DTOs;

// Request: a participant cannot be moved to another guide through an update
public class ParticipantUpdateDto
{
    [Required, StringLength(100, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    [Required, Phone, StringLength(20)]
    public string Phone { get; set; } = string.Empty;
}
