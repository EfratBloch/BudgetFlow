using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace YouthGroups.API.Middleware;

// The safety net for UNEXPECTED errors (database down, a bug). Expected errors never get here -
// the services return them as Result and ApiControllerBase turns them into 400/403/404/409.
// The caller gets a clean 500 in the same ProblemDetails format as every other error, with no
// stack trace or internal names; the full details go to the log, linked by the same traceId.
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;

        // The client closed the connection - nothing is wrong on our side and nobody is waiting for an answer
        if (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
        {
            _logger.LogInformation("Request {Method} {Path} was cancelled by the client. TraceId: {TraceId}",
                context.Request.Method, context.Request.Path, traceId);
            return true;
        }

        _logger.LogError(exception, "Unhandled exception on {Method} {Path}. TraceId: {TraceId}",
            context.Request.Method, context.Request.Path, traceId);

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Something went wrong",
            Detail = "An unexpected error occurred. Please try again later.",
            Instance = context.Request.Path
        };
        problem.Extensions["traceId"] = traceId;

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(problem, ct);
        return true;
    }
}
