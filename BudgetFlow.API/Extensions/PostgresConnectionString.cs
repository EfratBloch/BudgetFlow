using Npgsql;

namespace BudgetFlow.API.Extensions;

// Render (and most hosts) hand out the database as a URL: postgresql://user:password@host:port/database.
// Npgsql only understands the key/value form (Host=...;Username=...), so a URL is converted here.
// A key/value string - the form used in User Secrets - is returned as it is.
public static class PostgresConnectionString
{
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !(value.StartsWith("postgres://") || value.StartsWith("postgresql://")))
            return value;

        var uri = new Uri(value);
        var credentials = uri.UserInfo.Split(':', 2);

        return new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort || uri.Port <= 0 ? 5432 : uri.Port,
            Database = uri.AbsolutePath.TrimStart('/'),
            Username = Uri.UnescapeDataString(credentials[0]),
            Password = credentials.Length > 1 ? Uri.UnescapeDataString(credentials[1]) : null
        }.ConnectionString;
    }
}
