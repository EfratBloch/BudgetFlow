using BudgetFlow.Core.Common;
using BudgetFlow.Core.DTOs;

namespace BudgetFlow.Core.Interfaces.Services;

public interface IMeetingService
{
    // A guide sees only her own meetings; sending query.UserId as a guide is Forbidden
    Task<Result<PagedResult<MeetingListItemDto>>> GetPagedAsync(MeetingQueryDto query, CurrentUser caller, CancellationToken ct);

    Task<Result<MeetingDto>> GetByIdAsync(int id, CurrentUser caller, CancellationToken ct);

    // Validates, checks the budget pot, and saves the meeting, expenses and attendance in one transaction
    Task<Result<MeetingDto>> CreateAsync(MeetingCreateDto dto, CurrentUser caller, CancellationToken ct);

    // Owner only
    Task<Result> UpdateAsync(int id, MeetingUpdateDto dto, CurrentUser caller, CancellationToken ct);

    // Owner only. No balance to repair - balances are computed, never stored.
    Task<Result> DeleteAsync(int id, CurrentUser caller, CancellationToken ct);
}
