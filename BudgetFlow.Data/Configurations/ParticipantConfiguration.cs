using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BudgetFlow.Core.Entities;

namespace BudgetFlow.Data.Configurations;

public class ParticipantConfiguration : IEntityTypeConfiguration<Participant>
{
    public void Configure(EntityTypeBuilder<Participant> builder)
    {
        builder.ToTable("Participants");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.FullName).IsRequired().HasMaxLength(100);
        builder.Property(p => p.Phone).IsRequired().HasMaxLength(20);

        // One-to-many: a guide has many participants.
        // Restrict: a guide who still has participants cannot be deleted.
        builder.HasOne(p => p.Guide)
            .WithMany(u => u.Participants)
            .HasForeignKey(p => p.GuideId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
