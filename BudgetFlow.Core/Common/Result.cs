namespace BudgetFlow.Core.Common;

// Result of a service operation that returns no value (update, delete, return an item).
// Services return this instead of throwing or returning HTTP types, so they never know about HTTP.
public class Result
{
    public ResultStatus Status { get; }
    public bool IsSuccess => Status == ResultStatus.Ok;
    public string? Error { get; }

    // Extra data for the client, e.g. { shortfall, birthdayAvailable } on a budget conflict
    public IReadOnlyDictionary<string, object>? Details { get; }

    protected Result(ResultStatus status, string? error, IReadOnlyDictionary<string, object>? details)
    {
        Status = status;
        Error = error;
        Details = details;
    }

    public static Result Ok() => new(ResultStatus.Ok, null, null);
    public static Result NotFound(string error) => new(ResultStatus.NotFound, error, null);
    public static Result Validation(string error) => new(ResultStatus.Validation, error, null);
    public static Result Forbidden(string error) => new(ResultStatus.Forbidden, error, null);

    public static Result Conflict(string error, IReadOnlyDictionary<string, object>? details = null)
        => new(ResultStatus.Conflict, error, details);
}

// Result of a service operation that returns a value on success.
public class Result<T> : Result
{
    private readonly T? _value;

    // Reading Value of a failed result is a programming error, so it throws
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Cannot read Value of a failed result ({Status}).");

    private Result(ResultStatus status, T? value, string? error, IReadOnlyDictionary<string, object>? details)
        : base(status, error, details)
    {
        _value = value;
    }

    public static Result<T> Ok(T value) => new(ResultStatus.Ok, value, null, null);
    public static new Result<T> NotFound(string error) => new(ResultStatus.NotFound, default, error, null);
    public static new Result<T> Validation(string error) => new(ResultStatus.Validation, default, error, null);
    public static new Result<T> Forbidden(string error) => new(ResultStatus.Forbidden, default, error, null);

    public static new Result<T> Conflict(string error, IReadOnlyDictionary<string, object>? details = null)
        => new(ResultStatus.Conflict, default, error, details);
}
