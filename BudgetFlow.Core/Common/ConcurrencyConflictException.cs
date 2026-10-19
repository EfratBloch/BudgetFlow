namespace BudgetFlow.Core.Common;

// Thrown by the Data layer when SaveChanges hits a concurrency conflict
// (EF Core's DbUpdateConcurrencyException: the row changed between read and save).
// It lives in Core so that the Service layer can catch it without referencing EF Core.
public class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
