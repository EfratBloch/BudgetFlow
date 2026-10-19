namespace BudgetFlow.Core.Common;

// Claim names inside the JWT. AuthService writes them, and the API layer reads
// them back to build CurrentUser - one definition so the two never drift apart.
public static class AppClaimTypes
{
    public const string UserId = "userId";
    public const string Role = "role";
}
