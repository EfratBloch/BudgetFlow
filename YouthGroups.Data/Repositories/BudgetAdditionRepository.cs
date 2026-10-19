using Microsoft.EntityFrameworkCore;
using YouthGroups.Core.Entities;
using YouthGroups.Core.Enums;
using YouthGroups.Core.Interfaces.Repositories;

namespace YouthGroups.Data.Repositories;

public class BudgetAdditionRepository : Repository<BudgetAddition>, IBudgetAdditionRepository
{
    public BudgetAdditionRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<BudgetAddition>> GetAllAsync(int? guideId, CancellationToken ct)
        => await Set.AsNoTracking()
            .Include(a => a.Guide)
            .Where(a => guideId == null || a.GuideId == guideId)
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .ToListAsync(ct);

    // Read only, with the guide loaded so her name can be shown
    public override Task<BudgetAddition?> GetByIdAsync(int id, CancellationToken ct)
        => Set.AsNoTracking().Include(a => a.Guide).FirstOrDefaultAsync(a => a.Id == id, ct);

    // SUM in the database; the nullable cast turns "no rows" into null instead of an error
    public async Task<decimal> SumAddedAsync(int guideId, BudgetSource targetPot, CancellationToken ct)
        => await Set
            .Where(a => a.GuideId == guideId && a.TargetPot == targetPot)
            .SumAsync(a => (decimal?)a.Amount, ct) ?? 0;

    public async Task<decimal> SumTransferredFromAsync(BudgetSource fromPot, CancellationToken ct)
        => await Set
            .Where(a => a.Kind == BudgetAdditionKind.Transfer && a.FromPot == fromPot)
            .SumAsync(a => (decimal?)a.Amount, ct) ?? 0;
}
