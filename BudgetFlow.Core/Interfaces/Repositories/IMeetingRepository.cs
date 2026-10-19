using BudgetFlow.Core.Common;
using BudgetFlow.Core.DTOs;
using BudgetFlow.Core.Entities;
using BudgetFlow.Core.Enums;

namespace BudgetFlow.Core.Interfaces.Repositories;

public interface IMeetingRepository : IRepository<Meeting>
{
    // Filtered, sorted and paged inside the query. Rows are projected straight to
    // list items, so expenses and attendance are summed / counted in SQL.
    Task<PagedResult<MeetingListItemDto>> GetPagedAsync(MeetingQueryDto query, CancellationToken ct);

    // Read only, with User, Expenses and Participants loaded through Include
    Task<Meeting?> GetWithDetailsAsync(int id, CancellationToken ct);

    // Tracked, with Expenses and Participants - for update and delete
    Task<Meeting?> GetWithDetailsForUpdateAsync(int id, CancellationToken ct);

    // Total spent per pot by the meetings this user reported (SUM ... GROUP BY BudgetSource).
    // excludeMeetingId leaves out the meeting being updated, so its old expenses do not count twice.
    Task<IReadOnlyDictionary<BudgetSource, decimal>> GetSpentBySourceAsync(
        int userId, int? excludeMeetingId, CancellationToken ct);
}
