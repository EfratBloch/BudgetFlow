using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using BudgetFlow.Core.Entities;
using BudgetFlow.Core.Enums;

namespace BudgetFlow.Tests.Concurrency;

// The proof of the optimistic concurrency on Equipment.Version (xmin), against real PostgreSQL.
// Two guides read the same projector while it is Available, both mark it Taken, both save.
// The first save must win; the second must fail with DbUpdateConcurrencyException,
// because its UPDATE ... WHERE xmin = <the value it read> no longer matches any row.
public class EquipmentConcurrencyTests : IClassFixture<PostgresTestDatabase>
{
    private readonly PostgresTestDatabase _database;

    public EquipmentConcurrencyTests(PostgresTestDatabase database)
    {
        _database = database;
    }

    [Fact]
    public async Task TwoContextsTakeTheSameAvailableItem_SecondSaveThrowsDbUpdateConcurrencyException()
    {
        var (rachelId, shiraId, projectorId) = await SeedAsync();

        // Two separate contexts = two separate requests, each with its own connection
        await using var rachelContext = _database.CreateContext();
        await using var shiraContext = _database.CreateContext();

        var rachelCopy = await rachelContext.Equipment.SingleAsync(e => e.Id == projectorId);
        var shiraCopy = await shiraContext.Equipment.SingleAsync(e => e.Id == projectorId);

        // Both read it free, with the same xmin
        rachelCopy.Status.Should().Be(EquipmentStatus.Available);
        shiraCopy.Status.Should().Be(EquipmentStatus.Available);
        shiraCopy.Version.Should().Be(rachelCopy.Version);

        rachelCopy.Status = EquipmentStatus.Taken;
        rachelCopy.HolderUserId = rachelId;
        rachelCopy.TakenAt = DateTime.UtcNow;

        shiraCopy.Status = EquipmentStatus.Taken;
        shiraCopy.HolderUserId = shiraId;
        shiraCopy.TakenAt = DateTime.UtcNow;

        // The first save wins
        await rachelContext.SaveChangesAsync();

        // The second one updates zero rows and must throw
        var secondSave = () => shiraContext.SaveChangesAsync();
        await secondSave.Should().ThrowAsync<DbUpdateConcurrencyException>();

        // The database holds the first guide's take only
        await using var checkContext = _database.CreateContext();
        var saved = await checkContext.Equipment.AsNoTracking().SingleAsync(e => e.Id == projectorId);
        saved.Status.Should().Be(EquipmentStatus.Taken);
        saved.HolderUserId.Should().Be(rachelId);
        saved.Version.Should().NotBe(shiraCopy.Version);
    }

    private async Task<(int RachelId, int ShiraId, int ProjectorId)> SeedAsync()
    {
        await using var context = _database.CreateContext();

        var rachel = NewGuide("rachel@test.local", "Rachel", "111111111");
        var shira = NewGuide("shira@test.local", "Shira", "222222222");
        var projector = new Equipment { Name = "Projector 1", Status = EquipmentStatus.Available };

        context.Users.AddRange(rachel, shira);
        context.Equipment.Add(projector);
        await context.SaveChangesAsync();

        return (rachel.Id, shira.Id, projector.Id);
    }

    private static User NewGuide(string email, string name, string nationalId) => new()
    {
        Email = email,
        PasswordHash = "not-a-real-hash",
        Role = UserRole.Guide,
        FullName = name,
        Phone = "0500000000",
        NationalId = nationalId,
        CreatedAt = DateTime.UtcNow
    };
}
