using System.Diagnostics;

namespace YouthGroups.API.Middleware;

// Gives every request its own id (a new Guid), so all the log lines of one request can be found together.
// The id is kept in three places:
//  - HttpContext.Items   : for code later in the same request (e.g. the error handler puts it in its answer)
//  - the response header : the client sees it and can quote it when reporting a problem
//  - the logging scope   : every log line written during this request gets it (nlog.config prints it)
public class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-ID";
    public const string ItemKey = "CorrelationId";

    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Always made here, never taken from the client, so nobody can push their own text into our logs
        var correlationId = Guid.NewGuid().ToString();

        context.Items[ItemKey] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (_logger.BeginScope(new Dictionary<string, object> { [ItemKey] = correlationId }))
        {
            // Method and path only - never the body or the headers (passwords, tokens)
            _logger.LogInformation("Incoming request {Method} {Path}", context.Request.Method, context.Request.Path);

            var stopwatch = Stopwatch.StartNew();
            await _next(context);

            _logger.LogInformation("Finished {Method} {Path} with {StatusCode} in {ElapsedMs} ms",
                context.Request.Method, context.Request.Path, context.Response.StatusCode, stopwatch.ElapsedMilliseconds);
        }
    }
}
