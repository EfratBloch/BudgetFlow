using YouthGroups.Core.Enums;

namespace YouthGroups.Core.Common;

// The caller of a service method, taken by the API layer from the JWT claims.
// Services use it for ownership checks ("a guide may only touch her own data").
public sealed record CurrentUser(int UserId, UserRole Role)
{
    public bool IsCoordinator => Role == UserRole.Coordinator;
}
