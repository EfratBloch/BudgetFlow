namespace BudgetFlow.Core.DTOs;

// Response: a guide who attended a guides meeting
public class MeetingGuideDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
}
