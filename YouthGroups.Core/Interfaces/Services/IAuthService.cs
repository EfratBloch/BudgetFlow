using YouthGroups.Core.Common;
using YouthGroups.Core.DTOs;

namespace YouthGroups.Core.Interfaces.Services;

public interface IAuthService
{
    // Creates the coordinator account. Conflict if a coordinator, the email or the national id already exists.
    Task<Result<UserDto>> RegisterAsync(RegisterDto dto, CancellationToken ct);

    // Returns a JWT with UserId and Role claims. A wrong email or password gives the same error.
    Task<Result<TokenDto>> LoginAsync(LoginDto dto, CancellationToken ct);
}
