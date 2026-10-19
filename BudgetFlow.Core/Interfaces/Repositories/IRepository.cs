namespace BudgetFlow.Core.Interfaces.Repositories;

// Operations shared by every repository.
public interface IRepository<T> where T : class
{
    // Read only - the entity is not tracked, so changing it has no effect
    Task<T?> GetByIdAsync(int id, CancellationToken ct);

    // Tracked - use when the entity is about to be changed and saved.
    // For Equipment this is what makes the concurrency token work.
    Task<T?> GetForUpdateAsync(int id, CancellationToken ct);

    Task AddAsync(T entity, CancellationToken ct);

    // Update and Remove only mark the entity in EF's change tracker - no database
    // round trip happens here, so there is nothing to await. The write itself
    // happens in IUnitOfWork.SaveChangesAsync, which is async.
    void Update(T entity);
    void Remove(T entity);
}
