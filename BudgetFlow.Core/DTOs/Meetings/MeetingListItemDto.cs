using BudgetFlow.Core.Enums;

namespace BudgetFlow.Core.DTOs;

// Response: one row in GET /api/meetings - totals only, without the full lists
public class MeetingListItemDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string ReporterName { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public MeetingType Type { get; set; }
    public string Program { get; set; } = string.Empty;
    public string? EquipmentName { get; set; }
    public int ParticipantCount { get; set; }
    public decimal TotalExpenses { get; set; }
}
