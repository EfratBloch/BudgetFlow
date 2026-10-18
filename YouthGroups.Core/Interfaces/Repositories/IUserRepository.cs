using YouthGroups.Core.Common;
using YouthGroups.Core.Entities;

namespace YouthGroups.Core.Interfaces.Repositories;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email, CancellationToken ct);

    // excludeUserId lets an update keep its own email / national id
    Task<bool> EmailExistsAsync(string email, int? excludeUserId, CancellationToken ct);
    Task<bool> NationalIdExistsAsync(string nationalId, int? excludeUserId, CancellationToken ct);

    Task<bool> CoordinatorExistsAsync(CancellationToken ct);
    Task<User?> GetCoordinatorAsync(CancellationToken ct);

    // Guides only, paged and sorted inside the query (Skip/Take before ToListAsync)
    Task<PagedResult<User>> GetGuidesPagedAsync(string? sort, int page, int pageSize, CancellationToken ct);

    Task<int> CountGuidesAsync(CancellationToken ct);

    // Every guide with her participant count and spending per pot - ONE query
    Task<IReadOnlyList<GuideBudgetRow>> GetGuideBudgetRowsAsync(CancellationToken ct);
}
