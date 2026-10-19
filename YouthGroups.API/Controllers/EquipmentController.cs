using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YouthGroups.Core.DTOs;
using YouthGroups.Core.Enums;
using YouthGroups.Core.Interfaces.Services;

namespace YouthGroups.API.Controllers;

// The equipment store. Everyone logged in can see it and take or return items;
// adding, editing and deleting items is for the coordinator.
// Taking and returning are a separate resource - the item's holder - not a field in PUT.
[Route("api/equipment")]
public class EquipmentController : ApiControllerBase
{
    private const string Coordinator = nameof(UserRole.Coordinator);

    private readonly IEquipmentService _equipment;

    public EquipmentController(IEquipmentService equipment)
    {
        _equipment = equipment;
    }

    // GET /api/equipment?status=Available - status is optional
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<EquipmentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] EquipmentStatus? status, CancellationToken ct)
        => FromResult(await _equipment.GetAllAsync(status, ct));

    // GET /api/equipment/1 - with the holder's name
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(EquipmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
        => FromResult(await _equipment.GetByIdAsync(id, ct));

    // POST /api/equipment - a new item always starts as Available
    [HttpPost]
    [Authorize(Roles = Coordinator)]
    [ProducesResponseType(typeof(EquipmentDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(EquipmentCreateDto dto, CancellationToken ct)
        => FromCreated(await _equipment.CreateAsync(dto, ct), nameof(GetById), e => new { id = e.Id });

    // PUT /api/equipment/1 - name, description, maintenance (409 while the item is on loan)
    [HttpPut("{id:int}")]
    [Authorize(Roles = Coordinator)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(int id, EquipmentUpdateDto dto, CancellationToken ct)
        => FromResult(await _equipment.UpdateAsync(id, dto, ct));

    // DELETE /api/equipment/1 - 409 while the item is on loan
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Coordinator)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
        => FromResult(await _equipment.DeleteAsync(id, ct));

    // PUT /api/equipment/1/holder - take the item. When two guides take the same item at the
    // same moment, exactly one gets 204 and the other 409 (optimistic concurrency on xmin).
    [HttpPut("{id:int}/holder")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Take(int id, CancellationToken ct)
        => FromResult(await _equipment.TakeAsync(id, Caller, ct));

    // DELETE /api/equipment/1/holder - return the item (the holder or the coordinator)
    [HttpDelete("{id:int}/holder")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Return(int id, CancellationToken ct)
        => FromResult(await _equipment.ReturnAsync(id, Caller, ct));
}
