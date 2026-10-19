using BudgetFlow.Core.Enums;

namespace BudgetFlow.Core.Entities;

// Everyone who can log in: the coordinator or a guide.
public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string NationalId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } // UTC

    // A guide's own participants (empty for the coordinator)
    public ICollection<Participant> Participants { get; set; } = new List<Participant>();

    // Meetings this user reported
    public ICollection<Meeting> Meetings { get; set; } = new List<Meeting>();
}
