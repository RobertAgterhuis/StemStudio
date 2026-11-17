using System.Net;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.LibraryCare;
using StemStudio.Api.LibraryCare.Gate;
using StemStudio.Api.LibraryCare.Restore;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.LibraryCare;

namespace StemStudio.Api.Tests.LibraryCare;

/// <summary>013 CTO E2E finding: only start-up may create the live database file; fresh-item detection of the switch.</summary>
public sealed class LiveDatabaseFileTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("stemstudio-livedb-");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void ForLive_OpensAFileDatabaseReadWriteOnly()
    {
        var live = new SqliteConnectionStringBuilder(LiveDatabaseFile.ForLive("Data Source=x.db;Pooling=False"));

        (live.Mode, live.DataSource, live.Pooling).ShouldBe((SqliteOpenMode.ReadWrite, "x.db", false));
    }

    [Theory]
    [InlineData("Data Source=x.db;Mode=ReadOnly", SqliteOpenMode.ReadOnly)]
    [InlineData("Data Source=:memory:", SqliteOpenMode.ReadWriteCreate)]
    [InlineData("Data Source=shared;Mode=Memory", SqliteOpenMode.Memory)]
    [InlineData("Data Source=file:x.db?cache=shared", SqliteOpenMode.ReadWriteCreate)]
    [InlineData("Data Source=", SqliteOpenMode.ReadWriteCreate)]
    public void ForLive_KeepsAnExplicitModeAndNonFileDatabases(string configured, SqliteOpenMode mode) =>
        new SqliteConnectionStringBuilder(LiveDatabaseFile.ForLive(configured)).Mode.ShouldBe(mode);

    [Fact]
    public void LiveConnection_OnAMissingFile_FailsAndCreatesNothing()
    {
        var database = Path.Combine(_root.FullName, "stemstudio.db");
        using var connection = new SqliteConnection(LiveDatabaseFile.ForLive($"Data Source={database}"));

        Should.Throw<SqliteException>(connection.Open);

        File.Exists(database).ShouldBeFalse();
    }

    [Fact]
    public void EnsureCreated_CreatesTheFileOnceOnly()
    {
        var database = Path.Combine(_root.FullName, "sub", "stemstudio.db");
        var live = LiveDatabaseFile.ForLive($"Data Source={database}");

        LiveDatabaseFile.EnsureCreated(live).ShouldBeTrue();
        LiveDatabaseFile.EnsureCreated(live).ShouldBeFalse();
        LiveDatabaseFile.EnsureCreated($"Data Source={database}x").ShouldBeFalse();
        LiveDatabaseFile.EnsureCreated("Data Source=:memory:").ShouldBeFalse();

        File.Exists(database).ShouldBeTrue();
        File.Exists(database + "x").ShouldBeFalse();
    }

    [Fact]
    public async Task Api_WithTheDatabaseMovedAside_AnswersAnErrorWithoutCreatingAnEmptyDatabase()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        (await client.GetAsync(new Uri(ApiRoutes.Tracks, UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var database = Path.Combine(factory.DataDir, "stemstudio.db");
        SqliteConnection.ClearAllPools();
        File.Move(database, database + ".aside");

        using var response = await client.GetAsync(new Uri(ApiRoutes.Tracks, UriKind.Relative), Ct);
        using var status = await client.GetAsync(new Uri(ApiRoutes.LibraryCareStatus, UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        status.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        File.Exists(database).ShouldBeFalse();
        File.Move(database + ".aside", database);
    }

    [Fact]
    public async Task Status_DuringMaintenance_ReadsNoDatabaseAndCountsAsARequest()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var gate = factory.Services.GetRequiredService<LibraryGate>();
        var database = Path.Combine(factory.DataDir, "stemstudio.db");
        (await client.GetAsync(new Uri(ApiRoutes.Tracks, UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        gate.EnterMaintenance();
        SqliteConnection.ClearAllPools();
        File.Move(database, database + ".aside");

        using var response = await client.GetAsync(new Uri(ApiRoutes.LibraryCareStatus, UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.BodyAsync<LibraryCareStatusDto>();
        (body.Gate, body.ActiveWork.Count, body.LibraryEmpty).ShouldBe((GateModeDto.Maintenance, 0, false));
        gate.InFlight.ShouldBe(0);
        File.Exists(database).ShouldBeFalse();
        File.Move(database + ".aside", database);
        gate.ExitMaintenance();
    }

    [Fact]
    public void FreshItems_TreatOnlyEmptyThingsAsFresh()
    {
        var folder = Directory.CreateDirectory(Path.Combine(_root.FullName, "tracks")).FullName;
        FreshItems.IsFreshEmpty(folder).ShouldBeTrue();
        File.WriteAllText(Path.Combine(folder, "a.flac"), "x");
        FreshItems.IsFreshEmpty(folder).ShouldBeFalse();
        FreshItems.IsFreshEmpty(Path.Combine(_root.FullName, "missing")).ShouldBeFalse();

        var wal = Path.Combine(_root.FullName, "stemstudio.db-wal");
        var shm = Path.Combine(_root.FullName, "stemstudio.db-shm");
        File.WriteAllText(shm, "index");
        FreshItems.IsFreshEmpty(shm).ShouldBeTrue();
        File.WriteAllText(wal, string.Empty);
        (FreshItems.IsFreshEmpty(wal), FreshItems.IsFreshEmpty(shm)).ShouldBe((true, true));
        File.WriteAllText(wal, "frames");
        (FreshItems.IsFreshEmpty(wal), FreshItems.IsFreshEmpty(shm)).ShouldBe((false, false));

        var other = Path.Combine(_root.FullName, "uploads");
        File.WriteAllText(other, "data");
        FreshItems.IsFreshEmpty(other).ShouldBeFalse();
    }

    [Fact]
    public void FreshItems_ADatabaseIsFreshOnlyWithoutTables()
    {
        var database = Path.Combine(_root.FullName, "empty.db");
        Execute(database, "PRAGMA user_version=1");
        FreshItems.IsFreshEmpty(database).ShouldBeTrue();

        Execute(database, "CREATE TABLE Tracks (Id TEXT)");
        FreshItems.IsFreshEmpty(database).ShouldBeFalse();

        var text = Path.Combine(_root.FullName, "text.db");
        File.WriteAllText(text, "not a database at all, but someone's data");
        FreshItems.IsFreshEmpty(text).ShouldBeFalse();
    }

    [Fact]
    public async Task Mover_RefusesToClearData_AndGivesUpOnAPersistentLock()
    {
        var source = Path.Combine(_root.FullName, "a");
        var destination = Path.Combine(_root.FullName, "b");
        File.WriteAllText(source, "a");
        File.WriteAllText(destination, "b");
        var files = new FakeLibraryFiles { FailMove = _ => true };

        (await new JournalMover(new FakeLibraryFiles(), TimeSpan.Zero).MoveAsync(source, destination, [destination])).ShouldBeFalse();
        (await new JournalMover(files, TimeSpan.Zero).MoveAsync(source, Path.Combine(_root.FullName, "c"), [])).ShouldBeFalse();
        (await new JournalMover(files, TimeSpan.Zero).MoveAsync(Path.Combine(_root.FullName, "gone"), destination, [])).ShouldBeTrue();

        files.MoveAttempts.ShouldBe(LibrarySwitcher.MoveAttempts);
        (File.ReadAllText(source), File.ReadAllText(destination)).ShouldBe(("a", "b"));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        _root.Delete(recursive: true);
    }

    private static void Execute(string database, string sql)
    {
        using var connection = new SqliteConnection($"Data Source={database};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
