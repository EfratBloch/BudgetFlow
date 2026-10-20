using System.ComponentModel.DataAnnotations;

namespace BudgetFlow.Core.DTOs;

// Request: the guide comes from the route (/api/users/{id}/participants), not from the body
public class ParticipantCreateDto
{
    [Required, StringLength(100, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    [Required, Phone, StringLength(20)]
    public string Phone { get; set; } = string.Empty;
}
