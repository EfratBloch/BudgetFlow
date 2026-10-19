namespace BudgetFlow.Core.Settings;

// Bound to the "Jwt" section of configuration through IOptions<JwtSettings>.
// Issuer, Audience and ExpiryMinutes live in appsettings.json.
// Key is a secret: User Secrets in development, an environment variable in production.
public class JwtSettings
{
    public const string SectionName = "Jwt";

    // HMAC-SHA256 signing key - at least 32 bytes (256 bits)
    public string Key { get; set; } = string.Empty;

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int ExpiryMinutes { get; set; }
}
