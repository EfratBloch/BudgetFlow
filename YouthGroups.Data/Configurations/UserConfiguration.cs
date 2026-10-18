using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using YouthGroups.Core.Entities;

namespace YouthGroups.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email).IsRequired().HasMaxLength(100);
        builder.Property(u => u.PasswordHash).IsRequired();
        builder.Property(u => u.FullName).IsRequired().HasMaxLength(100);
        builder.Property(u => u.Phone).IsRequired().HasMaxLength(20);
        builder.Property(u => u.NationalId).IsRequired().HasMaxLength(9).IsFixedLength();

        // Stored as text ("Coordinator" / "Guide") so the table is readable
        builder.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);

        // No two users with the same email or national id - enforced by the database itself
        builder.HasIndex(u => u.Email).IsUnique();
        builder.HasIndex(u => u.NationalId).IsUnique();
    }
}
