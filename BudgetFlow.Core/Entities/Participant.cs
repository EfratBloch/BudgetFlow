namespace BudgetFlow.Core.Entities;

public class Participant
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string NationalId { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;

    public int GuideId { get; set; }
    public User Guide { get; set; } = null!;

    // Skip navigation: meetings she attended (join table MeetingAttendance, no join class)
    public ICollection<Meeting> Meetings { get; set; } = new List<Meeting>();
}
