using Microsoft.EntityFrameworkCore;
using BudgetFlow.Core.Common;
using BudgetFlow.Core.Entities;
using BudgetFlow.Core.Enums;
using BudgetFlow.Core.Interfaces.Repositories;

namespace BudgetFlow.Data.Repositories;

public class UserRepository : Repository<User>, IUserRepository
{
    public UserRepository(AppDbContext context) : base(context)
    {
    }

    public Task<User?> GetByEmailAsync(string email, CancellationToken ct)
        => Set.AsNoTracking().FirstOrDefaultAsync(u => u.Email == email, ct);

    public Task<bool> EmailExistsAsync(string email, int? excludeUserId, CancellationToken ct)
        => Set.AnyAsync(u => u.Email == email && (excludeUserId == null || u.Id != excludeUserId), ct);

    public Task<bool> NationalIdExistsAsync(string nationalId, int? excludeUserId, CancellationToken ct)
        => Set.AnyAsync(u => u.NationalId == nationalId && (excludeUserId == null || u.Id != excludeUserId), ct);

    public Task<bool> CoordinatorExistsAsync(CancellationToken ct)
        => Set.AnyAsync(u => u.Role == UserRole.Coordinator, ct);

    public Task<User?> GetCoordinatorAsync(CancellationToken ct)
        => Set.AsNoTracking().FirstOrDefaultAsync(u => u.Role == UserRole.Coordinator, ct);

    public async Task<PagedResult<User>> GetGuidesPagedAsync(string? sort, int page, int pageSize, CancellationToken ct)
    {
        var query = Set.AsNoTracking().Where(u => u.Role == UserRole.Guide);

        var totalCount = await query.CountAsync(ct);

        query = sort switch
        {
            "name_desc" => query.OrderByDescending(u => u.FullName),
            "created_asc" => query.OrderBy(u => u.CreatedAt),
            "created_desc" => query.OrderByDescending(u => u.CreatedAt),
            _ => query.OrderBy(u => u.FullName)
        };

        // Real pagination: Skip/Take become OFFSET/LIMIT in the SQL, before ToListAsync
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<User>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public Task<int> CountGuidesAsync(CancellationToken ct)
        => Set.CountAsync(u => u.Role == UserRole.Guide, ct);

    // One query: four EXISTS subqueries combined with OR
    public Task<bool> HasLinkedDataAsync(int userId, CancellationToken ct)
        => Set.AnyAsync(u => u.Id == userId
            && (u.Participants.Any()
                || u.Meetings.Any()
                || Context.Equipment.Any(e => e.HolderUserId == userId)
                || Context.BudgetAdditions.Any(a => a.GuideId == userId)), ct);

    // ONE query for all guides. The counts and sums are subqueries that PostgreSQL
    // computes itself - no loop over guides in C#, so no N+1.
    public async Task<IReadOnlyList<GuideBudgetRow>> GetGuideBudgetRowsAsync(CancellationToken ct)
        => await Set.AsNoTracking()
            .Where(u => u.Role == UserRole.Guide)
            .OrderBy(u => u.FullName)
            .Select(u => new GuideBudgetRow(
                u.Id,
                u.FullName,
                u.Participants.Count(),
                u.Meetings.SelectMany(m => m.Expenses)
                    .Where(e => e.BudgetSource == BudgetSource.Activity)
                    .Sum(e => (decimal?)e.Amount) ?? 0,
                u.Meetings.SelectMany(m => m.Expenses)
                    .Where(e => e.BudgetSource == BudgetSource.Birthday)
                    .Sum(e => (decimal?)e.Amount) ?? 0,
                Context.BudgetAdditions
                    .Where(a => a.GuideId == u.Id && a.TargetPot == BudgetSource.Activity)
                    .Sum(a => (decimal?)a.Amount) ?? 0,
                Context.BudgetAdditions
                    .Where(a => a.GuideId == u.Id && a.TargetPot == BudgetSource.Birthday)
                    .Sum(a => (decimal?)a.Amount) ?? 0))
            .ToListAsync(ct);
}
