using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BudgetFlow.Core.Entities;

namespace BudgetFlow.Data.Configurations;

public class MeetingConfiguration : IEntityTypeConfiguration<Meeting>
{
    public void Configure(EntityTypeBuilder<Meeting> builder)
    {
        builder.ToTable("Meetings");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Program).IsRequired().HasMaxLength(500);
        builder.Property(m => m.Notes).HasMaxLength(1000);
        builder.Property(m => m.Type).HasConversion<string>().HasMaxLength(20);

        // Speeds up "meetings of one user, newest first" - the most common list query
        builder.HasIndex(m => new { m.UserId, m.Date });

        // One-to-many: a user reports many meetings.
        // Restrict: a user who reported meetings cannot be deleted.
        builder.HasOne(m => m.User)
            .WithMany(u => u.Meetings)
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Optional: the store item used in the meeting.
        // SetNull: deleting the item keeps the meeting, without the item.
        builder.HasOne(m => m.Equipment)
            .WithMany()
            .HasForeignKey(m => m.EquipmentId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        // One-to-many: a meeting has many expenses.
        // Cascade: deleting a meeting deletes its expenses, and the balance grows back by itself.
        builder.HasMany(m => m.Expenses)
            .WithOne(e => e.Meeting)
            .HasForeignKey(e => e.MeetingId)
            .OnDelete(DeleteBehavior.Cascade);

        // Many-to-many through skip navigations: no join class in Core.
        // EF creates the join table MeetingAttendance with two columns.
        // A row there only says "participant X attended meeting Y", so it goes away
        // together with either side.
        builder.HasMany(m => m.Participants)
            .WithMany(p => p.Meetings)
            .UsingEntity<Dictionary<string, object>>(
                "MeetingAttendance",
                participant => participant.HasOne<Participant>().WithMany()
                    .HasForeignKey("ParticipantId").OnDelete(DeleteBehavior.Cascade),
                meeting => meeting.HasOne<Meeting>().WithMany()
                    .HasForeignKey("MeetingId").OnDelete(DeleteBehavior.Cascade),
                join => join.HasKey("MeetingId", "ParticipantId"));

        // Many-to-many: the guides who attended a guides meeting (join table MeetingGuideAttendance).
        // Like MeetingAttendance, a row goes away together with either side.
        builder.HasMany(m => m.Guides)
            .WithMany(u => u.AttendedMeetings)
            .UsingEntity<Dictionary<string, object>>(
                "MeetingGuideAttendance",
                guide => guide.HasOne<User>().WithMany()
                    .HasForeignKey("GuideId").OnDelete(DeleteBehavior.Cascade),
                meeting => meeting.HasOne<Meeting>().WithMany()
                    .HasForeignKey("MeetingId").OnDelete(DeleteBehavior.Cascade),
                join => join.HasKey("MeetingId", "GuideId"));
    }
}
