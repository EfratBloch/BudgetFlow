using System.ComponentModel.DataAnnotations;
using BudgetFlow.Core.Enums;

namespace BudgetFlow.Core.DTOs;

// Request: POST /api/meetings. The reporter is taken from the JWT, never from the body.
public class MeetingCreateDto
{
    // Nullable so that a missing value fails [Required] instead of silently becoming
    // the enum default (Activity) or DateTime.MinValue
    [Required]
    public MeetingType? Type { get; set; }

    [Required]
    public DateTime? Date { get; set; } // UTC

    [Required, StringLength(500, MinimumLength = 2)]
    public string Program { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Notes { get; set; }

    // Optional, Activity meetings only: the store item used. Does not take the item.
    public int? EquipmentId { get; set; }

    [Required]
    public List<int> ParticipantIds { get; set; } = new();

    // Guides meetings only: the guides who attended (participants stay empty)
    public List<int> GuideIds { get; set; } = new();

    [Required]
    public List<ExpenseCreateDto> Expenses { get; set; } = new();

    // false: going over the activity pot returns 409 with the shortfall.
    // true:  the guide approved charging the shortfall to her birthday pot
    //        (the rest of the activity pot is used first).
    public bool AllowBirthdayDraw { get; set; }
}
