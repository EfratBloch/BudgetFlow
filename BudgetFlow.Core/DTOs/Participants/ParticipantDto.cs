namespace BudgetFlow.Core.DTOs;

// Response
public class ParticipantDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string NationalId { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public int GuideId { get; set; }
}
