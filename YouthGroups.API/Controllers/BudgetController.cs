using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YouthGroups.Core.DTOs;
using YouthGroups.Core.Enums;
using YouthGroups.Core.Interfaces.Services;

namespace YouthGroups.API.Controllers;

// Budgets (always computed, never stored) and the coordinator's budget additions.
[Route("api")]
public class BudgetController : ApiControllerBase
{
    private const string Coordinator = nameof(UserRole.Coordinator);

    private readonly IBudgetService _budget;
    private readonly IBudgetAdditionService _additions;

    public BudgetController(IBudgetService budget, IBudgetAdditionService additions)
    {
        _budget = budget;
        _additions = additions;
    }

    // GET /api/users/2/budget - the two pots of one user (a guide only her own, 403 otherwise)
    [HttpGet("users/{userId:int}/budget")]
    [ProducesResponseType(typeof(UserBudgetDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserBudget(int userId, CancellationToken ct)
        => FromResult(await _budget.GetUserBudgetAsync(userId, Caller, ct));

    // GET /api/budget/me - the caller's own pots, taken from her token (no id to enter)
    [HttpGet("budget/me")]
    [ProducesResponseType(typeof(UserBudgetDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyBudget(CancellationToken ct)
        => FromResult(await _budget.GetUserBudgetAsync(Caller.UserId, Caller, ct));

    // GET /api/budget/summary - the coordinator and every guide in one call
    [HttpGet("budget/summary")]
    [Authorize(Roles = Coordinator)]
    [ProducesResponseType(typeof(BudgetSummaryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummary(CancellationToken ct)
        => FromResult(await _budget.GetSummaryAsync(ct));

    // GET /api/budget-additions?guideId=2 - all additions, or one guide's (newest first)
    [HttpGet("budget-additions")]
    [Authorize(Roles = Coordinator)]
    [ProducesResponseType(typeof(IReadOnlyList<BudgetAdditionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAdditions([FromQuery] int? guideId, CancellationToken ct)
        => FromResult(await _additions.GetAllAsync(guideId, ct));

    // POST /api/users/2/budget-additions - Kind "Extra" (new money) or "Transfer" with FromPot
    [HttpPost("users/{guideId:int}/budget-additions")]
    [Authorize(Roles = Coordinator)]
    [ProducesResponseType(typeof(BudgetAdditionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddMoney(int guideId, BudgetAdditionCreateDto dto, CancellationToken ct)
    {
        var result = await _additions.CreateAsync(guideId, dto, ct);

        // 201 with the list address, since a single addition has no address of its own
        return result.IsSuccess
            ? Created($"/api/budget-additions?guideId={guideId}", result.Value)
            : FromResult(result);
    }

    // DELETE /api/budget-additions/4 - cancels an addition; 409 if the guide already spent it
    [HttpDelete("budget-additions/{id:int}")]
    [Authorize(Roles = Coordinator)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelAddition(int id, CancellationToken ct)
        => FromResult(await _additions.DeleteAsync(id, ct));
}
