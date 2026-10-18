using Microsoft.EntityFrameworkCore;
using YouthGroups.Core.Entities;
using YouthGroups.Core.Enums;
using YouthGroups.Core.Interfaces.Services;

namespace YouthGroups.Data.Seed;

// Demo data, so the system can be used right after the database is created.
// The accounts below are documented in the README.
public class DbSeeder
{
    public const string CoordinatorEmail = "sarah@youthgroups.local";
    public const string CoordinatorPassword = "Coord!2026";
    public const string GuideOneEmail = "rachel@youthgroups.local";
    public const string GuideTwoEmail = "shira@youthgroups.local";
    public const string GuidePassword = "Guide!2026";

    private readonly AppDbContext _context;
    private readonly IPasswordHasher _passwordHasher;

    public DbSeeder(AppDbContext context, IPasswordHasher passwordHasher)
    {
        _context = context;
        _passwordHasher = passwordHasher;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        // Seed only an empty database - running twice never duplicates data
        if (await _context.Users.AnyAsync(ct))
            return;

        var now = DateTime.UtcNow;

        var sarah = NewUser(CoordinatorEmail, CoordinatorPassword, UserRole.Coordinator,
            "Sarah Levi", "050-1000001", "300000001", now);
        var rachel = NewUser(GuideOneEmail, GuidePassword, UserRole.Guide,
            "Rachel Cohen", "050-1000002", "300000002", now);
        var shira = NewUser(GuideTwoEmail, GuidePassword, UserRole.Guide,
            "Shira Mizrahi", "050-1000003", "300000003", now);

        // Five participants per guide
        var rachelGroup = NewParticipants(rachel, "Rachel's", 400000001);
        var shiraGroup = NewParticipants(shira, "Shira's", 400000101);

        _context.Users.AddRange(sarah, rachel, shira);
        _context.Participants.AddRange(rachelGroup);
        _context.Participants.AddRange(shiraGroup);

        // Only ONE projector, so the race between two guides can be demonstrated on it
        _context.Equipment.AddRange(
            new Equipment { Name = "Projector 1", Description = "Portable projector with HDMI cable", Status = EquipmentStatus.Available },
            new Equipment { Name = "PA system", Description = "Speaker and two wireless microphones", Status = EquipmentStatus.Available },
            new Equipment { Name = "Activity kit", Description = "Craft supplies and games", Status = EquipmentStatus.Available });

        // Sample meetings in the past, with expenses and attendance.
        // Each expense is charged to the pot that matches its meeting type.
        var day = now.Date.AddHours(16); // 16:00 UTC

        _context.Meetings.AddRange(
            NewMeeting(rachel, MeetingType.Activity, day.AddDays(-21), "Movement month opening activity",
                rachelGroup.Take(4), BudgetSource.Activity, (45m, "Sweets"), (15m, "Craft materials")),
            NewMeeting(rachel, MeetingType.Birthday, day.AddDays(-10), "Birthday party for Noa",
                rachelGroup, BudgetSource.Birthday, (12m, "Cake")),
            NewMeeting(shira, MeetingType.Activity, day.AddDays(-14), "Team building games",
                shiraGroup, BudgetSource.Activity, (35m, "Snacks")),
            NewMeeting(sarah, MeetingType.GuidesMeeting, day.AddDays(-7), "Monthly guides meeting",
                Enumerable.Empty<Participant>(), BudgetSource.GuideMeetings, (50m, "Refreshments")),
            NewMeeting(sarah, MeetingType.SpecialEvent, day.AddDays(-3), "Movement-wide evening",
                rachelGroup.Concat(shiraGroup), BudgetSource.Annual, (120m, "Pizza"), (30m, "Decorations")));

        await _context.SaveChangesAsync(ct);
    }

    private User NewUser(string email, string password, UserRole role,
        string fullName, string phone, string nationalId, DateTime createdAt) => new()
    {
        Email = email,
        PasswordHash = _passwordHasher.Hash(password), // never the plain password
        Role = role,
        FullName = fullName,
        Phone = phone,
        NationalId = nationalId,
        CreatedAt = createdAt
    };

    private static List<Participant> NewParticipants(User guide, string groupName, int firstNationalId)
    {
        string[] names = { "Noa", "Maya", "Tamar", "Yael", "Adi" };

        return names.Select((name, i) => new Participant
        {
            FullName = $"{name} ({groupName} group)",
            NationalId = (firstNationalId + i).ToString(),
            Phone = $"052-20000{firstNationalId % 1000 + i:00}",
            Guide = guide
        }).ToList();
    }

    private static Meeting NewMeeting(User reporter, MeetingType type, DateTime date, string program,
        IEnumerable<Participant> attendees, BudgetSource source, params (decimal Amount, string Description)[] expenses)
    {
        var meeting = new Meeting
        {
            User = reporter,
            Type = type,
            Date = date,
            Program = program
        };

        foreach (var participant in attendees)
            meeting.Participants.Add(participant);

        foreach (var (amount, description) in expenses)
            meeting.Expenses.Add(new Expense { Amount = amount, Description = description, BudgetSource = source });

        return meeting;
    }
}
