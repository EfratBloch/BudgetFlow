using System.ComponentModel.DataAnnotations;

namespace BudgetFlow.Core.DTOs;

// Request: PUT /api/meetings/{id}. Type is not editable - changing it would move the
// money to a different pot (and possibly to a pot that belongs to someone else).
public class MeetingUpdateDto
{
    [Required]
    public DateTime? Date { get; set; } // UTC

    [Required, StringLength(500, MinimumLength = 2)]
    public string Program { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Notes { get; set; }

    [Required]
    public List<int> ParticipantIds { get; set; } = new();

    // Replaces the meeting's expenses as a whole
    [Required]
    public List<ExpenseCreateDto> Expenses { get; set; } = new();

    public bool AllowBirthdayDraw { get; set; }
}
