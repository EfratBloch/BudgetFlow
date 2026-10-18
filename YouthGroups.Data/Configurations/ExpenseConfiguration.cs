using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using YouthGroups.Core.Entities;

namespace YouthGroups.Data.Configurations;

public class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        // The database itself rejects an amount of zero or less
        builder.ToTable("Expenses", t =>
            t.HasCheckConstraint("CK_Expenses_Amount_Positive", "\"Amount\" > 0"));

        builder.HasKey(e => e.Id);

        // numeric(10,2): exact money, up to 99,999,999.99
        builder.Property(e => e.Amount).HasPrecision(10, 2);
        builder.Property(e => e.Description).IsRequired().HasMaxLength(200);
        builder.Property(e => e.BudgetSource).HasConversion<string>().HasMaxLength(20);

        // The Meeting -> Expense relationship is configured in MeetingConfiguration
    }
}
