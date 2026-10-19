namespace BudgetFlow.Core.Common;

// Outcome of a service call. The API layer maps each value to one HTTP status:
// Ok -> 200/201/204, Validation -> 400, Forbidden -> 403, NotFound -> 404, Conflict -> 409.
public enum ResultStatus
{
    Ok,
    NotFound,
    Validation,
    Forbidden,
    Conflict
}
