using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using BudgetFlow.Data;

namespace BudgetFlow.Tests.Concurrency;

// A throwaway PostgreSQL database for the concurrency proof: created and migrated before the tests,
// dropped after them. Real PostgreSQL on purpose - EF InMemory and SQLite have no xmin, so they
// cannot show what the production database does.
//
// The connection comes from (first wins):
//   1. the BUDGETFLOW_TEST_CONNECTION environment variable
//   2. ConnectionStrings:Default in the API project's User Secrets (the one set in the README)
// The database name in it is always replaced by BudgetFlowDb_Test, so the real database is never touched.
// Nothing here is committed: no password lives in the source.
public class PostgresTestDatabase : IAsyncLifetime
{
    public const string DatabaseName = "BudgetFlowDb_Test";

    private const string EnvironmentVariable = "BUDGETFLOW_TEST_CONNECTION";
    private const string ApiUserSecretsId = "781b0234-c936-4a02-9815-569e54bd410f";

    public string ConnectionString { get; }

    public PostgresTestDatabase()
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets(ApiUserSecretsId)
            .Build();

        var baseConnection = Environment.GetEnvironmentVariable(EnvironmentVariable)
            ?? configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                $"No test database connection. Set the {EnvironmentVariable} environment variable, "
                + "or ConnectionStrings:Default in the API's User Secrets (see README).");

        ConnectionString = new NpgsqlConnectionStringBuilder(baseConnection) { Database = DatabaseName }.ConnectionString;
    }

    // A new, independent context - one per "user" in the race
    public AppDbContext CreateContext() => new(
        new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(ConnectionString).Options);

    public async Task InitializeAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync(); // left over from a run that crashed
        await context.Database.MigrateAsync();       // the real schema, including the xmin mapping
    }

    public async Task DisposeAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
    }
}
