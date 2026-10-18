using Microsoft.EntityFrameworkCore;
using YouthGroups.Core.Interfaces.Repositories;

namespace YouthGroups.Data.Repositories;

// Shared implementation of IRepository<T>. Every entity has an int "Id" key.
public class Repository<T> : IRepository<T> where T : class
{
    protected readonly AppDbContext Context;

    public Repository(AppDbContext context)
    {
        Context = context;
    }

    protected DbSet<T> Set => Context.Set<T>();

    // Read only - not tracked
    public virtual Task<T?> GetByIdAsync(int id, CancellationToken ct)
        => Set.AsNoTracking().FirstOrDefaultAsync(e => EF.Property<int>(e, "Id") == id, ct);

    // Tracked - EF remembers the original values (including xmin on Equipment)
    public virtual Task<T?> GetForUpdateAsync(int id, CancellationToken ct)
        => Set.FirstOrDefaultAsync(e => EF.Property<int>(e, "Id") == id, ct);

    public Task<bool> ExistsAsync(int id, CancellationToken ct)
        => Set.AnyAsync(e => EF.Property<int>(e, "Id") == id, ct);

    public async Task AddAsync(T entity, CancellationToken ct)
        => await Set.AddAsync(entity, ct);

    public void Update(T entity) => Set.Update(entity);

    public void Remove(T entity) => Set.Remove(entity);
}
