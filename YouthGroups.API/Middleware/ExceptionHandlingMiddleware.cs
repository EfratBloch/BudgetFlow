namespace YouthGroups.API.Middleware;

// The safety net for UNEXPECTED errors (database down, a bug). Expected errors never get here -
// the services return them as Result and ApiControllerBase turns them into 400/403/404/409.
// It is first in the pipeline, so a "try" around it covers everything that runs after it.
// The caller gets one consistent JSON shape, { traceId, status, message }, with no stack trace or
// internal names; the full details go to the log at Error level, linked by the same traceId.
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            // false = the answer had already started and cannot be replaced: let the server cut the connection
            if (!await TryHandleAsync(context, exception))
                throw;
        }
    }

    private async Task<bool> TryHandleAsync(HttpContext context, Exception exception)
    {
        // The id CorrelationIdMiddleware made for this request; it runs after us, so read it from Items
        var traceId = context.Items[CorrelationIdMiddleware.ItemKey] as string ?? context.TraceIdentifier;

        // The CorrelationId scope ended when the exception left CorrelationIdMiddleware - open it again,
        // so these log lines carry the id like every other line of the request
        using var scope = _logger.BeginScope(new Dictionary<string, object> { [CorrelationIdMiddleware.ItemKey] = traceId });

        // The client closed the connection - nothing is wrong on our side and nobody is waiting for an answer
        if (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
        {
            _logger.LogInformation("Request {Method} {Path} was cancelled by the client",
                context.Request.Method, context.Request.Path);
            return true;
        }

        _logger.LogError(exception, "Unhandled exception on {Method} {Path}",
            context.Request.Method, context.Request.Path);

        // Part of the answer was already sent - it cannot be replaced, only cut off
        if (context.Response.HasStarted)
            return false;

        // A request the server could not read (broken JSON, body too large) is the caller's mistake, not ours
        var (status, message) = exception is BadHttpRequestException badRequest
            ? (badRequest.StatusCode, "The request could not be read.")
            : (StatusCodes.Status500InternalServerError, "An unexpected error occurred. Please try again later.");

        context.Response.Clear(); // drop any headers a controller set before failing
        context.Response.Headers[CorrelationIdMiddleware.HeaderName] = traceId;
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new ErrorResponse(traceId, status, message));
        return true;
    }
}

// The body of every answer from ExceptionHandlingMiddleware (sent as camelCase JSON)
public record ErrorResponse(string TraceId, int Status, string Message);
