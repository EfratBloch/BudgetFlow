namespace YouthGroups.Core.Interfaces.Repositories;

// Saves and transactions, without exposing EF Core to the Service layer.
// All repositories in one request share the same DbContext, so one SaveChangesAsync
// writes everything they changed.
public interface IUnitOfWork
{
    // Throws ConcurrencyConflictException when a concurrency token no longer matches
    Task<int> SaveChangesAsync(CancellationToken ct);

    Task<ITransaction> BeginTransactionAsync(CancellationToken ct);
}

// A database transaction. Disposing without CommitAsync rolls it back.
public interface ITransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken ct);
    Task RollbackAsync(CancellationToken ct);
}
