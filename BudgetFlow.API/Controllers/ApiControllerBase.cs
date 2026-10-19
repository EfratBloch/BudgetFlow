using Microsoft.AspNetCore.Mvc;
using BudgetFlow.API.Extensions;
using BudgetFlow.Core.Common;

namespace BudgetFlow.API.Controllers;

// Base class of every controller. It turns a service Result into an HTTP response,
// so every controller answers in the same way and the services never know about HTTP:
//   Ok          -> 200 with the value, 201 for a create, 204 when there is no value
//   Validation  -> 400    Forbidden -> 403    NotFound -> 404    Conflict -> 409
// Errors are returned as ProblemDetails (the standard JSON error format, RFC 9457),
// with the service's Details (e.g. shortfall, birthdayAvailable) as extra fields.
[ApiController]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    // The caller, read from the JWT claims
    protected CurrentUser Caller => User.ToCurrentUser();

    // For an operation that returns a value: 200 with the value
    protected IActionResult FromResult<T>(Result<T> result)
        => result.IsSuccess ? Ok(result.Value) : Problem(result);

    // For an operation that returns nothing: 204 No Content
    protected IActionResult FromResult(Result result)
        => result.IsSuccess ? NoContent() : Problem(result);

    // For a create: 201 with the new item and a Location header pointing at its GET action
    protected IActionResult FromCreated<T>(Result<T> result, string actionName, Func<T, object> routeValues)
        => result.IsSuccess
            ? CreatedAtAction(actionName, routeValues(result.Value), result.Value)
            : Problem(result);

    private ObjectResult Problem(Result result)
    {
        var (status, title) = result.Status switch
        {
            ResultStatus.Validation => (StatusCodes.Status400BadRequest, "Invalid request"),
            ResultStatus.Forbidden => (StatusCodes.Status403Forbidden, "Forbidden"),
            ResultStatus.NotFound => (StatusCodes.Status404NotFound, "Not found"),
            ResultStatus.Conflict => (StatusCodes.Status409Conflict, "Conflict"),
            _ => throw new InvalidOperationException($"Result status {result.Status} is not an error.")
        };

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = result.Error,
            Instance = HttpContext.Request.Path
        };

        if (result.Details is not null)
        {
            foreach (var (key, value) in result.Details)
                problem.Extensions[key] = value;
        }

        return new ObjectResult(problem) { StatusCode = status };
    }
}
