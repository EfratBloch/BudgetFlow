using BudgetFlow.Core.Entities;
using BudgetFlow.Core.Enums;

namespace BudgetFlow.Core.Interfaces.Repositories;

public interface IBudgetAdditionRepository : IRepository<BudgetAddition>
{
    // Read only, with the guide loaded, newest first; guideId null = every guide
    Task<IReadOnlyList<BudgetAddition>> GetAllAsync(int? guideId, CancellationToken ct);

    // Sum of the additions to one pot of one guide (0 when there are none)
    Task<decimal> SumAddedAsync(int guideId, BudgetSource targetPot, CancellationToken ct);

    // Sum of the transfers out of one coordinator pot (0 when there are none)
    Task<decimal> SumTransferredFromAsync(BudgetSource fromPot, CancellationToken ct);
}
