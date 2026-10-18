using YouthGroups.Core.Enums;

namespace YouthGroups.Core.Entities;

public class Meeting
{
    public int Id { get; set; }

    // Who reported it - a guide or the coordinator
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public DateTime Date { get; set; } // always UTC
    public MeetingType Type { get; set; }
    public string Program { get; set; } = string.Empty;
    public string? Notes { get; set; }

    public ICollection<Expense> Expenses { get; set; } = new List<Expense>();

    // Skip navigation: attendance (join table MeetingAttendance, no join class)
    public ICollection<Participant> Participants { get; set; } = new List<Participant>();
}
