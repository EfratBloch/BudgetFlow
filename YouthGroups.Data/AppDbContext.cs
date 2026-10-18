using Microsoft.EntityFrameworkCore;
using YouthGroups.Core.Entities;

namespace YouthGroups.Data;

// The connection to the database. Every repository in a request shares one instance
// (registered as Scoped), so a single SaveChangesAsync writes all their changes together.
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    // One DbSet per entity - each becomes a table
    public DbSet<User> Users => Set<User>();
    public DbSet<Participant> Participants => Set<Participant>();
    public DbSet<Meeting> Meetings => Set<Meeting>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<Equipment> Equipment => Set<Equipment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Picks up every IEntityTypeConfiguration class in this project (the Configurations folder)
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
