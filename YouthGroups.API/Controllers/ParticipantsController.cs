using Microsoft.AspNetCore.Mvc;
using YouthGroups.Core.DTOs;
using YouthGroups.Core.Interfaces.Services;

namespace YouthGroups.API.Controllers;

// Participants. Open to every logged-in user; ParticipantService decides what is hers:
// a guide only her own participants, the coordinator anyone's (403 otherwise).
// A participant belongs to a guide, so listing and adding go through the guide's address.
[Route("api")]
public class ParticipantsController : ApiControllerBase
{
    private readonly IParticipantService _participants;

    public ParticipantsController(IParticipantService participants)
    {
        _participants = participants;
    }

    // GET /api/users/2/participants - one guide's participants
    [HttpGet("users/{guideId:int}/participants")]
    [ProducesResponseType(typeof(IReadOnlyList<ParticipantDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByGuide(int guideId, CancellationToken ct)
        => FromResult(await _participants.GetByGuideAsync(guideId, Caller, ct));

    // POST /api/users/2/participants - adds a participant to that guide's group
    [HttpPost("users/{guideId:int}/participants")]
    [ProducesResponseType(typeof(ParticipantDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(int guideId, ParticipantCreateDto dto, CancellationToken ct)
        => FromCreated(await _participants.CreateAsync(guideId, dto, Caller, ct), nameof(GetById), p => new { id = p.Id });

    // GET /api/participants/7
    [HttpGet("participants/{id:int}")]
    [ProducesResponseType(typeof(ParticipantDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
        => FromResult(await _participants.GetByIdAsync(id, Caller, ct));

    // PUT /api/participants/7 - name, national id and phone; never moves her to another guide
    [HttpPut("participants/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(int id, ParticipantUpdateDto dto, CancellationToken ct)
        => FromResult(await _participants.UpdateAsync(id, dto, Caller, ct));

    // DELETE /api/participants/7 - her attendance rows go with her
    [HttpDelete("participants/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
        => FromResult(await _participants.DeleteAsync(id, Caller, ct));
}
