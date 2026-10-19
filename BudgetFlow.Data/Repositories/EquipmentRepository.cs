using Microsoft.EntityFrameworkCore;
using BudgetFlow.Core.Entities;
using BudgetFlow.Core.Enums;
using BudgetFlow.Core.Interfaces.Repositories;

namespace BudgetFlow.Data.Repositories;

public class EquipmentRepository : Repository<Equipment>, IEquipmentRepository
{
    public EquipmentRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<Equipment>> GetAllAsync(EquipmentStatus? status, CancellationToken ct)
    {
        var query = Set.AsNoTracking().Include(e => e.Holder).AsQueryable();

        if (status.HasValue)
            query = query.Where(e => e.Status == status.Value);

        return await query.OrderBy(e => e.Name).ToListAsync(ct);
    }

    // Read only, with the holder loaded so her name can be shown
    public override Task<Equipment?> GetByIdAsync(int id, CancellationToken ct)
        => Set.AsNoTracking().Include(e => e.Holder).FirstOrDefaultAsync(e => e.Id == id, ct);

    // GetForUpdateAsync (tracked, no AsNoTracking) is inherited unchanged - that is what
    // take and return use, so the xmin value read here goes into the UPDATE's WHERE clause.
}
