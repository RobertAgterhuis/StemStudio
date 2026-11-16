using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StemStudio.Persistence.Agent;

namespace StemStudio.Persistence.Tests.Agent;

/// <summary>011 follow-up claude-key T011: the <c>AgentSecrets</c> table and <see cref="AgentSecretStore"/> (CK-FR-002).</summary>
public sealed class AgentSecretStoreTests
{
    private const string LastMigrationOf011 = "20261116064402_AddAgent";
    private const string Name = AgentSecretEntity.ClaudeApiKeyName;
    private static readonly DateTime T0 = TestData.T0;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Find_WithoutARow_ReturnsNull()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        await using var context = db.NewContext();

        new AgentSecretStore(context).Find(Name).ShouldBeNull();
    }

    [Fact]
    public async Task Upsert_InsertsTheRowAndReportsNotReplaced()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        await using (var write = db.NewContext())
        {
            (await new AgentSecretStore(write).UpsertAsync(Secret("cipher-1", "STO1", T0), Ct)).ShouldBeFalse();
        }

        await using var read = db.NewContext();
        var row = new AgentSecretStore(read).Find(Name).ShouldNotBeNull();
        (row.Name, row.Ciphertext, row.LastFour, row.PurposeVersion, row.StoredAt).ShouldBe((Name, "cipher-1", "STO1", 1, T0));
        row.StoredAt.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Fact]
    public async Task Upsert_OverAnExistingRow_ReplacesCiphertextLastFourAndTime()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        await using (var first = db.NewContext())
        {
            await new AgentSecretStore(first).UpsertAsync(Secret("cipher-1", "STO1", T0), Ct);
        }

        await using (var second = db.NewContext())
        {
            (await new AgentSecretStore(second).UpsertAsync(Secret("cipher-2", "STO2", T0.AddHours(1)), Ct)).ShouldBeTrue();
        }

        (await db.ScalarAsync("SELECT COUNT(*) FROM AgentSecrets")).ShouldBe("1");
        await using var read = db.NewContext();
        var row = new AgentSecretStore(read).Find(Name).ShouldNotBeNull();
        (row.Ciphertext, row.LastFour, row.StoredAt).ShouldBe(("cipher-2", "STO2", T0.AddHours(1)));
    }

    [Fact]
    public async Task Upsert_ThatFails_LeavesTheStoredRowUnchanged()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        await using (var first = db.NewContext())
        {
            await new AgentSecretStore(first).UpsertAsync(Secret("cipher-1", "STO1", T0), Ct);
        }

        await using (var second = db.NewContext())
        {
            await Should.ThrowAsync<DbUpdateException>(() => new AgentSecretStore(second).UpsertAsync(Secret("cipher-2", "TOO-LONG", T0), Ct));
        }

        await using var read = db.NewContext();
        var row = new AgentSecretStore(read).Find(Name).ShouldNotBeNull();
        (row.Ciphertext, row.LastFour).ShouldBe(("cipher-1", "STO1"));
    }

    [Fact]
    public async Task Upsert_UsedTwiceOnOneContext_ReplacesTheTrackedRow()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        await using var context = db.NewContext();
        var store = new AgentSecretStore(context);

        (await store.UpsertAsync(Secret("cipher-1", "STO1", T0), Ct)).ShouldBeFalse();
        (await store.UpsertAsync(Secret("cipher-2", "STO2", T0.AddMinutes(1)), Ct)).ShouldBeTrue();

        (await db.ScalarAsync("SELECT Ciphertext FROM AgentSecrets")).ShouldBe("cipher-2");
    }

    [Fact]
    public async Task Reprotect_UpdatesOnlyCiphertextAndTime()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        await using (var write = db.NewContext())
        {
            await new AgentSecretStore(write).UpsertAsync(Secret("cipher-1", "STO1", T0), Ct);
        }

        await using (var update = db.NewContext())
        {
            new AgentSecretStore(update).Reprotect(Name, "cipher-new", T0.AddDays(90)).ShouldBeTrue();
        }

        await using var read = db.NewContext();
        var row = new AgentSecretStore(read).Find(Name).ShouldNotBeNull();
        (row.Ciphertext, row.LastFour, row.PurposeVersion, row.StoredAt).ShouldBe(("cipher-new", "STO1", 1, T0.AddDays(90)));
    }

    [Fact]
    public async Task Reprotect_WithoutARow_ReturnsFalseAndAddsNothing()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        await using var context = db.NewContext();

        new AgentSecretStore(context).Reprotect(Name, "cipher-new", T0).ShouldBeFalse();
        (await db.ScalarAsync("SELECT COUNT(*) FROM AgentSecrets")).ShouldBe("0");
    }

    [Fact]
    public async Task Delete_RemovesTheRowAndIsIdempotent()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        await using (var write = db.NewContext())
        {
            await new AgentSecretStore(write).UpsertAsync(Secret("cipher-1", "STO1", T0), Ct);
        }

        await using var context = db.NewContext();
        var store = new AgentSecretStore(context);
        (await store.DeleteAsync(Name, Ct)).ShouldBeTrue();
        (await store.DeleteAsync(Name, Ct)).ShouldBeFalse();
        store.Find(Name).ShouldBeNull();
        (await db.ScalarAsync("SELECT COUNT(*) FROM AgentSecrets")).ShouldBe("0");
    }

    [Theory]
    [InlineData("name")]
    [InlineData("ciphertext")]
    [InlineData("last-four-short")]
    [InlineData("last-four-long")]
    public async Task ColumnLimits_AreEnforcedByTheDatabase(string violation)
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        await using var context = db.NewContext();
        var secret = violation switch
        {
            "name" => Secret("cipher", "STO1", T0, name: new string('n', AgentSecretEntity.NameMaxLength + 1)),
            "ciphertext" => Secret(new string('c', AgentSecretEntity.CiphertextMaxLength + 1), "STO1", T0),
            "last-four-short" => Secret("cipher", "ST1", T0),
            _ => Secret("cipher", "STO12", T0),
        };

        await Should.ThrowAsync<DbUpdateException>(() => new AgentSecretStore(context).UpsertAsync(secret, Ct));
    }

    [Fact]
    public async Task ColumnLimits_AcceptTheLargestValues()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        await using var context = db.NewContext();
        var secret = Secret(new string('c', AgentSecretEntity.CiphertextMaxLength), "STO1", T0, name: new string('n', AgentSecretEntity.NameMaxLength));

        (await new AgentSecretStore(context).UpsertAsync(secret, Ct)).ShouldBeFalse();
        (AgentSecretEntity.NameMaxLength, AgentSecretEntity.CiphertextMaxLength, AgentSecretEntity.LastFourLength).ShouldBe((40, 2048, 4));
    }

    [Fact]
    public async Task Arguments_AreValidated()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        await using var context = db.NewContext();
        var store = new AgentSecretStore(context);

        Should.Throw<ArgumentNullException>(() => new AgentSecretStore(null!));
        await Should.ThrowAsync<ArgumentNullException>(() => store.UpsertAsync(null!, Ct));
        Should.Throw<ArgumentException>(() => store.Find(" "));
        Should.Throw<ArgumentException>(() => store.Reprotect(Name, "", T0));
        await Should.ThrowAsync<ArgumentException>(() => store.DeleteAsync("", Ct));
    }

    [Fact]
    public async Task DatabaseFrom011_MigratesToTheSecretsTableAndKeepsItsData()
    {
        var path = Path.Combine(Path.GetTempPath(), $"stemstudio-011-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={path};Pooling=False";
        try
        {
            await SqliteTestDatabase.MigrateLegacyAsync(connectionString, LastMigrationOf011);

            await using var db = await SqliteTestDatabase.CreateAsync(connectionString);
            await using var read = db.NewContext();
            read.Database.GetPendingMigrations().ShouldBeEmpty();
            (await db.ScalarAsync("SELECT COUNT(*) FROM Tracks")).ShouldBe("1");
            (await db.ScalarAsync("SELECT COUNT(*) FROM AgentSecrets")).ShouldBe("0");
            (await new AgentSecretStore(read).UpsertAsync(Secret("cipher-1", "STO1", T0), Ct)).ShouldBeFalse();
            (await db.ScalarAsync("SELECT LastFour FROM AgentSecrets")).ShouldBe("STO1");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    private static AgentSecretEntity Secret(string ciphertext, string lastFour, DateTime storedAt, string name = Name) =>
        new() { Name = name, Ciphertext = ciphertext, LastFour = lastFour, PurposeVersion = 1, StoredAt = storedAt };
}
