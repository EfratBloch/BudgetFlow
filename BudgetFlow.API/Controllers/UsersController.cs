using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BudgetFlow.Core.Common;
using BudgetFlow.Core.DTOs;
using BudgetFlow.Core.Enums;
using BudgetFlow.Core.Interfaces.Services;

namespace BudgetFlow.API.Controllers;

// Guides management. Role checks are here (coordinator only); "is it her own?" checks are in UserService.
[Route("api/users")]
public class UsersController : ApiControllerBase
{
    private const string Coordinator = nameof(UserRole.Coordinator);

    private readonly IUserService _users;
    private readonly IGuideOverviewService _overview;

    public UsersController(IUserService users, IGuideOverviewService overview)
    {
        _users = users;
        _overview = overview;
    }

    // GET /api/users/2/overview - the guide panel: details, participants, both pots with
    // what is left, and her 10 most recent meetings, in one response.
    // The coordinator sees any guide; a guide only herself (checked in the service).
    [HttpGet("{id:int}/overview")]
    [ProducesResponseType(typeof(GuideOverviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOverview(int id, CancellationToken ct)
        => FromResult(await _overview.GetAsync(id, Caller, ct));

    // GET /api/users/me/overview - the same panel for the caller, taken from her token.
    // "me" is not an int, so it never matches the {id:int} route above.
    [HttpGet("me/overview")]
    [ProducesResponseType(typeof(GuideOverviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyOverview(CancellationToken ct)
        => FromResult(await _overview.GetAsync(Caller.UserId, Caller, ct));

    // GET /api/users?sort=name_asc&page=1&pageSize=20 - one page of guides
    [HttpGet]
    [Authorize(Roles = Coordinator)]
    [ProducesResponseType(typeof(PagedResult<UserDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGuides([FromQuery] UserQueryDto query, CancellationToken ct)
        => FromResult(await _users.GetGuidesAsync(query, ct));

    // GET /api/users/5 - the coordinator sees anyone, a guide only herself (403 otherwise)
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
        => FromResult(await _users.GetByIdAsync(id, Caller, ct));

    // POST /api/users - the coordinator adds a guide with an initial password
    [HttpPost]
    [Authorize(Roles = Coordinator)]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(UserCreateDto dto, CancellationToken ct)
        => FromCreated(await _users.CreateGuideAsync(dto, ct), nameof(GetById), u => new { id = u.Id });

    // PUT /api/users/5 - details only, never the password
    [HttpPut("{id:int}")]
    [Authorize(Roles = Coordinator)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(int id, UserUpdateDto dto, CancellationToken ct)
        => FromResult(await _users.UpdateAsync(id, dto, ct));

    // DELETE /api/users/5 - 409 while she still has participants, meetings, an item or additions
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Coordinator)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
        => FromResult(await _users.DeleteAsync(id, ct));
}
