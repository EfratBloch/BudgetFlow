using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BudgetFlow.Core.Common;
using BudgetFlow.Core.DTOs;
using BudgetFlow.Core.Interfaces.Services;

namespace BudgetFlow.API.Controllers;

// Register the coordinator and log in. Both are open - the caller has no token yet.
[Route("api/auth")]
[AllowAnonymous]
public class AuthController : ApiControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth)
    {
        _auth = auth;
    }

    // POST /api/auth/register - creates THE coordinator account (once; 409 afterwards)
    [HttpPost("register")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(RegisterDto dto, CancellationToken ct)
    {
        var result = await _auth.RegisterAsync(dto, ct);

        return result.IsSuccess
            ? Created($"/api/users/{result.Value.Id}", result.Value)
            : FromResult(result);
    }

    // POST /api/auth/login - returns a JWT to send as "Authorization: Bearer <token>"
    [HttpPost("login")]
    [ProducesResponseType(typeof(TokenDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(LoginDto dto, CancellationToken ct)
    {
        var result = await _auth.LoginAsync(dto, ct);

        // A wrong email or password is "not authenticated" (401), not a malformed request (400)
        if (result.Status == ResultStatus.Validation)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Unauthorized", detail: result.Error);

        return FromResult(result);
    }
}
