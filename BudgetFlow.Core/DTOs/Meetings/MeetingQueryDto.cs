using System.ComponentModel.DataAnnotations;
using BudgetFlow.Core.Enums;

namespace BudgetFlow.Core.DTOs;

// Request (query string) for GET /api/meetings
public class MeetingQueryDto
{
    // Coordinator only - a guide who sends it gets 403 (checked in the Service layer)
    public int? UserId { get; set; }

    public MeetingType? Type { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }

    // date_desc (default), date_asc
    [RegularExpression("^date_(asc|desc)$", ErrorMessage = "Sort must be date_asc or date_desc.")]
    public string? Sort { get; set; }

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}
