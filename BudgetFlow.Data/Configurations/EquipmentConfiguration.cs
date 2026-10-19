using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BudgetFlow.Core.Entities;

namespace BudgetFlow.Data.Configurations;

public class EquipmentConfiguration : IEntityTypeConfiguration<Equipment>
{
    public void Configure(EntityTypeBuilder<Equipment> builder)
    {
        builder.ToTable("Equipment");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Description).HasMaxLength(500);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);

        // Optional relationship: null while the item is in the store.
        // Restrict: a user who is holding an item cannot be deleted.
        builder.HasOne(e => e.Holder)
            .WithMany()
            .HasForeignKey(e => e.HolderUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // THE CONCURRENCY TOKEN.
        // Every PostgreSQL row has a hidden system column, xmin: the id of the transaction
        // that last wrote the row. The database changes it on every UPDATE - we never set it.
        // A uint property marked IsRowVersion() is mapped by Npgsql to xmin (no new column
        // is created), and EF Core adds it to the WHERE clause of every UPDATE:
        //     UPDATE "Equipment" SET ... WHERE "Id" = 3 AND xmin = 847
        // If another request changed the row first, xmin is no longer 847, zero rows are
        // updated, and EF Core throws DbUpdateConcurrencyException.
        // (Npgsql 10 removed UseXminAsConcurrencyToken(); this is its replacement.
        //  Not [Timestamp] byte[] RowVersion - that is SQL Server only.)
        builder.Property(e => e.Version).IsRowVersion();
    }
}
