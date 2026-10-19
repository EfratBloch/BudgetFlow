using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BudgetFlow.Core.Entities;

namespace BudgetFlow.Data.Configurations;

public class BudgetAdditionConfiguration : IEntityTypeConfiguration<BudgetAddition>
{
    public void Configure(EntityTypeBuilder<BudgetAddition> builder)
    {
        // The database itself rejects an amount of zero or less, and a transfer
        // without a source pot (or an extra amount with one)
        builder.ToTable("BudgetAdditions", t =>
        {
            t.HasCheckConstraint("CK_BudgetAdditions_Amount_Positive", "\"Amount\" > 0");
            t.HasCheckConstraint("CK_BudgetAdditions_FromPot_Matches_Kind",
                "(\"Kind\" = 'Transfer' AND \"FromPot\" IS NOT NULL) OR (\"Kind\" = 'Extra' AND \"FromPot\" IS NULL)");
        });

        builder.HasKey(a => a.Id);

        // numeric(10,2): exact money, the same as Expenses.Amount
        builder.Property(a => a.Amount).HasPrecision(10, 2);
        builder.Property(a => a.Reason).IsRequired().HasMaxLength(200);

        // Stored as text so the table is readable
        builder.Property(a => a.TargetPot).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.FromPot).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.Kind).HasConversion<string>().HasMaxLength(20);

        // Restrict: a guide who received additions cannot be deleted
        builder.HasOne(a => a.Guide)
            .WithMany()
            .HasForeignKey(a => a.GuideId)
            .OnDelete(DeleteBehavior.Restrict);

        // The pot sums filter by guide and pot
        builder.HasIndex(a => new { a.GuideId, a.TargetPot });
    }
}
