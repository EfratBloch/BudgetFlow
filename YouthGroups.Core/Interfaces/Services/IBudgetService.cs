using YouthGroups.Core.Common;
using YouthGroups.Core.DTOs;

namespace YouthGroups.Core.Interfaces.Services;

// Every budget is computed on each call from appsettings constants, counts and expense sums.
public interface IBudgetService
{
    // The coordinator sees anyone's pots; a guide only her own
    Task<Result<UserBudgetDto>> GetUserBudgetAsync(int userId, CurrentUser caller, CancellationToken ct);

    // The coordinator and every guide, without a query per guide
    Task<Result<BudgetSummaryDto>> GetSummaryAsync(CancellationToken ct);
}
