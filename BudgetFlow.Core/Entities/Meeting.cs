using BudgetFlow.Core.Enums;

namespace BudgetFlow.Core.Entities;

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

    // Optional: the store item used in an Activity meeting. Only a record -
    // it does not take the item or change its status.
    public int? EquipmentId { get; set; }
    public Equipment? Equipment { get; set; }

    public ICollection<Expense> Expenses { get; set; } = new List<Expense>();

    // Skip navigation: attendance (join table MeetingAttendance, no join class)
    public ICollection<Participant> Participants { get; set; } = new List<Participant>();

    // Skip navigation: the guides who attended a guides meeting (join table MeetingGuideAttendance).
    // A guides meeting is attended by guides, never by participants.
    public ICollection<User> Guides { get; set; } = new List<User>();
}
