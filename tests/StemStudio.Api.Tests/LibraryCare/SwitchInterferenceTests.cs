using Microsoft.Data.Sqlite;
using StemStudio.Api.LibraryCare.Gate;
using StemStudio.Api.LibraryCare.Jobs;
using StemStudio.Api.LibraryCare.Restore;
using StemStudio.Contracts.LibraryCare;

namespace StemStudio.Api.Tests.LibraryCare;

/// <summary>
/// 013 CTO E2E finding: a SQLite connection opened on the live path while the database was moved aside created an empty
/// live database (<c>Mode=ReadWriteCreate</c>); the move-in and the rollback then left the real database aside and the
/// journal <c>RolledBack</c>. The switch, the rollback and start-up recovery must never lose the real database.
/// </summary>
public sealed class SwitchInterferenceTests : IDisposable
{
    private static readonly Guid JobId = Guid.Parse("2b3c4d5e-6f70-4a8b-9c0d-1e2f3a4b5c6d");
    private readonly SwitchLibrary _library = new(JobId);
    private readonly FakeLibraryFiles _files = new();
    private readonly LibraryGate _gate = new(TimeProvider.System);
    private readonly StepHook _hook = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private string LiveDb => _library.Path("stemstudio.db");

    [Fact]
    public async Task EmptyDatabaseCreatedDuringTheSwitch_IsReplacedByTheRestoredOne()
    {
        _hook.After = index => index == 0 ? QueryWithDefaultMode(LiveDb) : null;

        await Switcher().SwitchAsync(JournalPlans.Restore(JobId, JobId, _library.Paths, keepPrevious: true), Ct);

        _library.Read("stemstudio.db").ShouldBe("restored db");
        File.Exists(_library.Path("stemstudio.db-wal")).ShouldBeFalse();
        File.Exists(_library.Path("stemstudio.db-shm")).ShouldBeFalse();
        _library.Read($"library-care/previous/{JobId}/stemstudio.db").ShouldBe("live db");
        (await Journal().FindAsync(JobId, Ct))!.State.ShouldBe(JournalState.Committed);
    }

    [Fact]
    public async Task EmptyDatabaseCreatedBeforeAFailedMove_IsReplacedByTheOriginalOnRollback()
    {
        _hook.After = index => index == 0 ? QueryWithDefaultMode(LiveDb) : null;
        _files.FailMove = source => source.EndsWith("assets", StringComparison.Ordinal);

        var error = await Should.ThrowAsync<LibraryJobException>(() => Switcher().SwitchAsync(JournalPlans.Restore(JobId, JobId, _library.Paths, keepPrevious: true), Ct));

        error.Code.ShouldBe(LibraryJobErrorCodeDto.IoError);
        _library.Read("stemstudio.db").ShouldBe("live db");
        _library.Read("tracks/a/stems/vocals.flac").ShouldBe("live stem");
        File.Exists(_library.Path($"library-care/previous/{JobId}/stemstudio.db")).ShouldBeFalse();
        (await Journal().FindAsync(JobId, Ct))!.State.ShouldBe(JournalState.RolledBack);
    }

    [Fact]
    public async Task RealDataAtTheLivePath_IsNeverDeleted_AndTheJournalStaysOpen()
    {
        _hook.After = index => index == 0 ? () => File.WriteAllText(LiveDb, "someone else's data") : null;
        _files.FailMove = source => source.EndsWith("assets", StringComparison.Ordinal);

        var error = await Should.ThrowAsync<LibraryJobException>(() => Switcher().SwitchAsync(JournalPlans.Restore(JobId, JobId, _library.Paths, keepPrevious: true), Ct));

        error.Code.ShouldBe(LibraryJobErrorCodeDto.IoError);
        _library.Read("stemstudio.db").ShouldBe("someone else's data");
        _library.Read($"library-care/previous/{JobId}/stemstudio.db").ShouldBe("live db");
        (await Journal().FindAsync(JobId, Ct))!.State.ShouldBe(JournalState.Switching);
        _gate.Mode.ShouldBe(LibraryGateMode.Open);
        error.Message.ShouldBe(LibrarySwitcher.NotRestoredText);

        // Start-up recovery refuses to continue while the real database is still aside; once the way is free it finishes.
        (await Should.ThrowAsync<InvalidOperationException>(() => Journal().RecoverAsync(_files, Ct))).Message.ShouldBe(RestoreJournal.RecoveryIncompleteText);
        _library.Read($"library-care/previous/{JobId}/stemstudio.db").ShouldBe("live db");
        File.Move(LiveDb, _library.Path("elsewhere.db"));
        (await Journal().RecoverAsync(_files, Ct)).ShouldBe(1);
        _library.Read("stemstudio.db").ShouldBe("live db");
        (await Journal().FindAsync(JobId, Ct))!.State.ShouldBe(JournalState.RolledBack);
    }

    [Fact]
    public async Task RecoveryOfARolledBackJournal_WhoseMoveKeepsFailing_LeavesItAside()
    {
        _library.Write($"library-care/previous/{JobId}/stemstudio.db", "live db");
        File.Delete(LiveDb);
        await Journal().SaveAsync(ObservedJournal(), Ct);
        _files.FailMove = _ => true;

        (await new RestoreJournal(_library.Paths, TimeSpan.Zero).RecoverAsync(_files, Ct)).ShouldBe(0);

        _library.Read($"library-care/previous/{JobId}/stemstudio.db").ShouldBe("live db");
    }

    [Fact]
    public async Task RecoveryOfASwitchingJournal_WithAnInterferingEmptyLiveDatabase_RestoresTheOriginal()
    {
        var plan = JournalPlans.Restore(JobId, JobId, _library.Paths, keepPrevious: true);
        _hook.After = index => index == 0 ? QueryWithDefaultMode(LiveDb) : index == plan.Steps.Count - 1 ? () => throw new SimulatedCrashException() : null;
        await Should.ThrowAsync<SimulatedCrashException>(() => Switcher().SwitchAsync(plan, Ct));

        (await Journal().RecoverAsync(_files, Ct)).ShouldBe(1);

        _library.Read("stemstudio.db").ShouldBe("live db");
        _library.Read("tracks/a/stems/vocals.flac").ShouldBe("live stem");
        _library.Read("assets/x/audio.flac").ShouldBe("live asset");
        _library.Read($"library-care/staging/{JobId}/stemstudio.db").ShouldBe("restored db");
        (await Journal().FindAsync(JobId, Ct))!.State.ShouldBe(JournalState.RolledBack);
    }

    [Fact]
    public async Task RecoveryOfARolledBackJournal_WithTheDatabaseStillAside_PutsItBack()
    {
        // The observed state: every MoveAside done, the rollback skipped the database because an empty one had been created.
        _library.Write($"library-care/previous/{JobId}/stemstudio.db", "live db");
        File.Delete(LiveDb);
        QueryWithDefaultMode(LiveDb)!();
        await Journal().SaveAsync(ObservedJournal(), Ct);

        (await Journal().RecoverAsync(_files, Ct)).ShouldBe(1);

        _library.Read("stemstudio.db").ShouldBe("live db");
        File.Exists(_library.Path($"library-care/previous/{JobId}/stemstudio.db")).ShouldBeFalse();
        (await Journal().RecoverAsync(_files, Ct)).ShouldBe(0);
    }

    [Fact]
    public async Task RecoveryOfARolledBackJournal_NeverReplacesARealLiveDatabase()
    {
        _library.Write($"library-care/previous/{JobId}/stemstudio.db", "stale aside copy");
        await Journal().SaveAsync(ObservedJournal(), Ct);

        (await Journal().RecoverAsync(_files, Ct)).ShouldBe(0);

        _library.Read("stemstudio.db").ShouldBe("live db");
        _library.Read($"library-care/previous/{JobId}/stemstudio.db").ShouldBe("stale aside copy");
    }

    [Fact]
    public async Task Undo_WithAnEmptyDatabaseCreatedDuringTheSwitch_BringsThePreviousLibraryBack()
    {
        await Switcher().SwitchAsync(JournalPlans.Restore(JobId, JobId, _library.Paths, keepPrevious: true), Ct);
        var undoId = Guid.NewGuid();
        _hook.After = index => index == 0 ? QueryWithDefaultMode(LiveDb) : null;

        await Switcher().SwitchAsync(JournalPlans.Undo(JobId, undoId, _library.Paths), Ct);

        _library.Read("stemstudio.db").ShouldBe("live db");
        _library.Read("tracks/a/stems/vocals.flac").ShouldBe("live stem");
        _library.Read($"library-care/previous/{undoId}/stemstudio.db").ShouldBe("restored db");
    }

    public void Dispose() => _library.Dispose();

    /// <summary>
    /// What the API did before the fix: a pooled connection on the default (create) mode while the database is aside, in WAL
    /// mode like the live database, then a query that fails with "no such table" (a database without tables, -wal and -shm remain).
    /// </summary>
    private static Action? QueryWithDefaultMode(string database) => () =>
    {
        using var connection = new SqliteConnection($"Data Source={database}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode=WAL";
        command.ExecuteScalar();
        command.CommandText = "SELECT COUNT(*) FROM Tracks";
        Should.Throw<SqliteException>(() => command.ExecuteScalar());
    };

    private static RestoreJournalRecord ObservedJournal() => new()
    {
        RestoreId = JobId,
        Kind = "undo",
        State = JournalState.RolledBack,
        KeepPrevious = true,
        Steps =
        [
            new JournalStep(JournalOp.MoveAside, "stemstudio.db", $"library-care/previous/{JobId}/stemstudio.db", true),
            new JournalStep(JournalOp.MoveIn, $"library-care/previous/{Guid.Empty}/stemstudio.db", "stemstudio.db", false),
        ],
    };

    private RestoreJournal Journal() => new(_library.Paths);

    private LibrarySwitcher Switcher() => new(_gate, Journal(), _files, _hook, TimeSpan.Zero);

    private sealed class StepHook : IJournalStepHook
    {
        public Func<int, Action?> After { get; set; } = _ => null;

        public void AfterStep(int index, LibraryGate gate) => After(index)?.Invoke();
    }
}
