using BudgetFlow.Core.Enums;

namespace BudgetFlow.Core.DTOs;

// Response: GET /api/meetings/{id} - one meeting with its expenses and attendance
public class MeetingDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string ReporterName { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public MeetingType Type { get; set; }
    public string Program { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public int? EquipmentId { get; set; }
    public string? EquipmentName { get; set; }
    public decimal TotalExpenses { get; set; }
    public List<ExpenseDto> Expenses { get; set; } = new();
    public List<ParticipantDto> Participants { get; set; } = new();
    public List<MeetingGuideDto> Guides { get; set; } = new();
}
