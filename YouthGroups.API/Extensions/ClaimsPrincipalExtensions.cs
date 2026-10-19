using System.Security.Claims;
using YouthGroups.Core.Common;
using YouthGroups.Core.Enums;

namespace YouthGroups.API.Extensions;

public static class ClaimsPrincipalExtensions
{
    // Builds the caller from the JWT claims that AuthService wrote (AppClaimTypes).
    // Controllers pass the result to the services for "is this hers?" checks.
    public static CurrentUser ToCurrentUser(this ClaimsPrincipal principal)
    {
        var userId = principal.FindFirstValue(AppClaimTypes.UserId);
        var role = principal.FindFirstValue(AppClaimTypes.Role);

        // Only reachable with a token this server signed, so a missing claim is a bug
        if (!int.TryParse(userId, out var id) || !Enum.TryParse<UserRole>(role, out var userRole))
            throw new InvalidOperationException("The token does not carry valid userId and role claims.");

        return new CurrentUser(id, userRole);
    }
}
