using Microsoft.AspNetCore.Mvc;
using YouthGroups.Core.Common;
using YouthGroups.Core.DTOs;
using YouthGroups.Core.Interfaces.Services;

namespace YouthGroups.API.Controllers;

// Meeting reports. Open to every logged-in user; MeetingService runs the ordered checks
// (date, type vs. role, participants, budget) and the "owner only" rule for changes.
// The reporter is always the caller from the token - never a field in the body.
[Route("api/meetings")]
public class MeetingsController : ApiControllerBase
{
    private readonly IMeetingService _meetings;

    public MeetingsController(IMeetingService meetings)
    {
        _meetings = meetings;
    }

    // GET /api/meetings?userId=2&type=Activity&from=2026-09-01&to=2026-10-31&sort=date_desc&page=1&pageSize=20
    // Paged in the database. A guide always gets her own meetings; sending userId as a guide is 403.
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<MeetingListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPaged([FromQuery] MeetingQueryDto query, CancellationToken ct)
        => FromResult(await _meetings.GetPagedAsync(query, Caller, ct));

    // GET /api/meetings/12 - with expenses and attendance
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(MeetingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
        => FromResult(await _meetings.GetByIdAsync(id, Caller, ct));

    // POST /api/meetings - over the activity budget returns 409 with shortfall and birthdayAvailable;
    // send it again with "allowBirthdayDraw": true to charge the shortfall to the birthday budget
    [HttpPost]
    [ProducesResponseType(typeof(MeetingDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(MeetingCreateDto dto, CancellationToken ct)
        => FromCreated(await _meetings.CreateAsync(dto, Caller, ct), nameof(GetById), m => new { id = m.Id });

    // PUT /api/meetings/12 - owner only; the type cannot change; expenses are replaced as a whole
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(int id, MeetingUpdateDto dto, CancellationToken ct)
        => FromResult(await _meetings.UpdateAsync(id, dto, Caller, ct));

    // DELETE /api/meetings/12 - owner only; the balance updates by itself (it is computed)
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
        => FromResult(await _meetings.DeleteAsync(id, Caller, ct));
}
