using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YouthGroups.Core.Common;
using YouthGroups.Core.DTOs;
using YouthGroups.Core.Enums;
using YouthGroups.Core.Interfaces.Services;

namespace YouthGroups.API.Controllers;

// Guides management. Role checks are here (coordinator only); "is it her own?" checks are in UserService.
[Route("api/users")]
public class UsersController : ApiControllerBase
{
    private const string Coordinator = nameof(UserRole.Coordinator);

    private readonly IUserService _users;

    public UsersController(IUserService users)
    {
        _users = users;
    }

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
