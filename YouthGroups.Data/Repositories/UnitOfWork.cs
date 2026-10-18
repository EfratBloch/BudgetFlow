using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using YouthGroups.Core.Common;
using YouthGroups.Core.Interfaces.Repositories;

namespace YouthGroups.Data.Repositories;

// Saves and transactions over the request's single AppDbContext.
// This is the only place that knows about EF Core's concurrency exception.
public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;

    public UnitOfWork(AppDbContext context)
    {
        _context = context;
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            return await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // The UPDATE matched zero rows because the concurrency token (xmin) changed
            // after we read the row. Translate it into a Core exception so the Service
            // layer can catch it without referencing EF Core.
            throw new ConcurrencyConflictException(
                "The row was changed by another request after it was read.", ex);
        }
    }

    public async Task<ITransaction> BeginTransactionAsync(CancellationToken ct)
    {
        var transaction = await _context.Database.BeginTransactionAsync(ct);
        return new EfTransaction(transaction);
    }

    // Wraps EF Core's transaction behind the Core ITransaction interface
    private sealed class EfTransaction : ITransaction
    {
        private readonly IDbContextTransaction _transaction;

        public EfTransaction(IDbContextTransaction transaction)
        {
            _transaction = transaction;
        }

        public Task CommitAsync(CancellationToken ct) => _transaction.CommitAsync(ct);

        public Task RollbackAsync(CancellationToken ct) => _transaction.RollbackAsync(ct);

        // Disposing a transaction that was not committed rolls it back
        public ValueTask DisposeAsync() => _transaction.DisposeAsync();
    }
}
