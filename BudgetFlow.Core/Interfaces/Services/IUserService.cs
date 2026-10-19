using BudgetFlow.Core.Common;
using BudgetFlow.Core.DTOs;

namespace BudgetFlow.Core.Interfaces.Services;

// Guides management. Role checks are on the controller; "self" checks are here.
public interface IUserService
{
    Task<Result<PagedResult<UserDto>>> GetGuidesAsync(UserQueryDto query, CancellationToken ct);

    // The coordinator sees anyone; a guide only herself (otherwise Forbidden)
    Task<Result<UserDto>> GetByIdAsync(int id, CurrentUser caller, CancellationToken ct);

    Task<Result<UserDto>> CreateGuideAsync(UserCreateDto dto, CancellationToken ct);
    Task<Result> UpdateAsync(int id, UserUpdateDto dto, CancellationToken ct);
    Task<Result> DeleteAsync(int id, CancellationToken ct);
}
