using BudgetFlow.Core.Common;
using BudgetFlow.Core.DTOs;

namespace BudgetFlow.Core.Interfaces.Services;

// The coordinator adds money to a guide's pot. Coordinator only - the role check is on the controller.
public interface IBudgetAdditionService
{
    // guideId null = every guide
    Task<Result<IReadOnlyList<BudgetAdditionDto>>> GetAllAsync(int? guideId, CancellationToken ct);

    // A transfer may not exceed what is left in the coordinator pot it comes from
    Task<Result<BudgetAdditionDto>> CreateAsync(int guideId, BudgetAdditionCreateDto dto, CancellationToken ct);

    // Cancels an addition. Conflict if the guide already spent that money.
    Task<Result> DeleteAsync(int id, CancellationToken ct);
}
