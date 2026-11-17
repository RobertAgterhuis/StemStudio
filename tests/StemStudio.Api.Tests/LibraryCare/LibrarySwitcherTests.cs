using StemStudio.Api.LibraryCare.Gate;
using StemStudio.Api.LibraryCare.Jobs;
using StemStudio.Api.LibraryCare.Restore;
using StemStudio.Api.Storage;
using StemStudio.Contracts.LibraryCare;

namespace StemStudio.Api.Tests.LibraryCare;

/// <summary>013 T046: the journalled library switch, crash recovery, undo and discard (FR-012, FR-013, FR-017, SC-005).</summary>
public sealed class LibrarySwitcherTests : IDisposable
{
    private static readonly Guid JobId = Guid.Parse("1a2b3c4d-5e6f-4a7b-8c9d-0e1f2a3b4c5d");
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("stemstudio-switch-");
    private readonly FakeLibraryFiles _files = new();
    private readonly LibraryGate _gate = new(TimeProvider.System);
    private readonly LibraryCarePaths _paths;
    private readonly CrashHook _hook = new();

    public LibrarySwitcherTests()
    {
        _paths = new LibraryCarePaths(new DataDirectory(System.IO.Path.Combine(_root.FullName, "data")), System.IO.Path.Combine(_root.FullName, "backups"));
        Write("stemstudio.db", "live db");
        Write("tracks/a/stems/vocals.flac", "live stem");
        Write("assets/x/audio.flac", "live asset");
        Write($"library-care/staging/{JobId}/stemstudio.db", "restored db");
        Write($"library-care/staging/{JobId}/files/tracks/b/stems/drums.flac", "restored stem");
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Replace_MovesTheLiveLibraryAsideAndTheStagedOneIn()
    {
        var plan = JournalPlans.Restore(JobId, JobId, _paths, keepPrevious: true);

        await Switcher().SwitchAsync(plan, Ct);

        Read("stemstudio.db").ShouldBe("restored db");
        Read("tracks/b/stems/drums.flac").ShouldBe("restored stem");
        File.Exists(Path("tracks/a/stems/vocals.flac")).ShouldBeFalse();
        File.Exists(Path("assets/x/audio.flac")).ShouldBeFalse();
        Read($"library-care/previous/{JobId}/tracks/a/stems/vocals.flac").ShouldBe("live stem");
        Read($"library-care/previous/{JobId}/stemstudio.db").ShouldBe("live db");
        (await new RestoreJournal(_paths).FindAsync(JobId, Ct))!.State.ShouldBe(JournalState.Committed);
        _gate.Mode.ShouldBe(LibraryGateMode.Open);
        _hook.MaintenanceSeen.ShouldBeTrue();
    }

    [Fact]
    public async Task EmptyLibrary_DiscardsTheMovedAsideFiles()
    {
        var plan = JournalPlans.Restore(JobId, JobId, _paths, keepPrevious: false);

        await Switcher().SwitchAsync(plan, Ct);

        Read("stemstudio.db").ShouldBe("restored db");
        Directory.Exists(_paths.Previous(JobId)).ShouldBeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task CrashAfterAnyStep_IsRolledBackAtStartUp(int step)
    {
        var plan = JournalPlans.Restore(JobId, JobId, _paths, keepPrevious: true);
        _hook.CrashAfter = step;

        await Should.ThrowAsync<SimulatedCrashException>(() => Switcher().SwitchAsync(plan, Ct));
        var recovered = await new RestoreJournal(_paths).RecoverAsync(_files, Ct);

        recovered.ShouldBe(1);
        Read("stemstudio.db").ShouldBe("live db");
        Read("tracks/a/stems/vocals.flac").ShouldBe("live stem");
        Read("assets/x/audio.flac").ShouldBe("live asset");
        Read($"library-care/staging/{JobId}/files/tracks/b/stems/drums.flac").ShouldBe("restored stem");
        (await new RestoreJournal(_paths).FindAsync(JobId, Ct))!.State.ShouldBe(JournalState.RolledBack);
    }

    [Fact]
    public async Task FailingMove_IsRetriedThenRolledBackAsIoError()
    {
        _files.FailMove = source => source.EndsWith("assets", StringComparison.Ordinal);
        var plan = JournalPlans.Restore(JobId, JobId, _paths, keepPrevious: true);

        var error = await Should.ThrowAsync<LibraryJobException>(() => Switcher().SwitchAsync(plan, Ct));

        error.Code.ShouldBe(LibraryJobErrorCodeDto.IoError);
        _files.MoveAttempts.ShouldBeGreaterThanOrEqualTo(LibrarySwitcher.MoveAttempts);
        Read("stemstudio.db").ShouldBe("live db");
        Read("tracks/a/stems/vocals.flac").ShouldBe("live stem");
        _gate.Mode.ShouldBe(LibraryGateMode.Open);
    }

    // Security review 013 item 9: requests still in flight after the idle timeout stop the switch before anything moves.
    [Fact]
    public async Task RequestsStillInFlight_FailTheSwitchWithoutMovingAnything()
    {
        var clock = new Microsoft.Extensions.Time.Testing.FakeTimeProvider();
        var gate = new LibraryGate(clock);
        using var request = gate.TrackRequest();
        var switcher = new LibrarySwitcher(gate, new RestoreJournal(_paths), _files, _hook, TimeSpan.Zero);

        var switching = switcher.SwitchAsync(JournalPlans.Restore(JobId, JobId, _paths, keepPrevious: true), Ct);
        while (!switching.IsCompleted)
        {
            clock.Advance(TimeSpan.FromSeconds(1));
            await Task.Delay(5, Ct);
        }

        (await Should.ThrowAsync<LibraryJobException>(() => switching)).Code.ShouldBe(LibraryJobErrorCodeDto.IoError);
        (Read("stemstudio.db"), Read("tracks/a/stems/vocals.flac")).ShouldBe(("live db", "live stem"));
        _files.MoveAttempts.ShouldBe(0);
        gate.Mode.ShouldBe(LibraryGateMode.Open);
        (await new RestoreJournal(_paths).FindAsync(JobId, Ct))!.State.ShouldBe(JournalState.RolledBack);
    }

    // Security review 013 item 10: a crash after the commit but before the previous library was recorded is finished at start-up.
    [Fact]
    public async Task CommittedJournalWithoutItsPreviousRecord_IsCompletedAtStartUp()
    {
        var previous = new PreviousLibraryDto
        {
            RestoreId = JobId,
            MovedAsideAt = DateTimeOffset.UnixEpoch,
            SizeBytes = 0,
            Counts = new ArchiveCountsDto { Tracks = 1, Mixes = 0, Assets = 1, Generations = 0, Styles = 0, LyricsSheets = 0, VoiceProfiles = 0 },
            RestoredArchive = "x.zip",
        };
        await Switcher().SwitchAsync(JournalPlans.Restore(JobId, JobId, _paths, keepPrevious: true) with { Previous = previous }, Ct);
        var store = new PreviousLibraryStore(_paths);
        (await store.ReadAsync(Ct)).ShouldBeNull();

        var journal = new RestoreJournal(_paths);
        (await journal.RecoverAsync(_files, Ct)).ShouldBe(0);
        (await journal.CompleteCommittedAsync(Ct)).ShouldBe(1);

        var recorded = (await store.ReadAsync(Ct)).ShouldNotBeNull();
        (recorded.RestoreId, recorded.RestoredArchive, recorded.Counts.Tracks).ShouldBe((JobId, "x.zip", 1));
        recorded.SizeBytes.ShouldBeGreaterThan(0);
        (await journal.CompleteCommittedAsync(Ct)).ShouldBe(0);
    }

    [Fact]
    public async Task CommittedUndoJournal_RemovesTheEmptiedPreviousLibraryAtStartUp()
    {
        await Switcher().SwitchAsync(JournalPlans.Restore(JobId, JobId, _paths, keepPrevious: true), Ct);
        var emptied = _paths.Previous(JobId);
        Write($"library-care/previous/{JobId}/previous.json", "{}");
        var undo = Guid.NewGuid();
        var journal = new RestoreJournal(_paths);
        await journal.SaveAsync(JournalPlans.Undo(JobId, undo, _paths) with { State = JournalState.Committed }, Ct);

        (await journal.CompleteCommittedAsync(Ct)).ShouldBe(1);

        Directory.Exists(emptied).ShouldBeFalse();
    }

    [Fact]
    public async Task Undo_SwapsBackAndKeepsTheRestoredLibraryAsTheNewPrevious()
    {
        await Switcher().SwitchAsync(JournalPlans.Restore(JobId, JobId, _paths, keepPrevious: true), Ct);
        var undoId = Guid.NewGuid();

        await Switcher().SwitchAsync(JournalPlans.Undo(JobId, undoId, _paths), Ct);

        Read("stemstudio.db").ShouldBe("live db");
        Read("tracks/a/stems/vocals.flac").ShouldBe("live stem");
        Read($"library-care/previous/{undoId}/tracks/b/stems/drums.flac").ShouldBe("restored stem");
        Directory.Exists(_paths.Previous(JobId)).ShouldBeFalse();
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("library-care/jobs/x")]
    [InlineData("library-care/previous/not-a-guid/tracks")]
    [InlineData("tracks/../..")]
    [InlineData("config")]
    public void JournalPaths_OutsideTheAllowedItemsAreRefused(string path)
    {
        JournalPlans.IsAllowedPath(path).ShouldBeFalse();
        Should.Throw<LibraryJobException>(() => JournalPlans.Validate(new RestoreJournalRecord
        {
            RestoreId = JobId,
            Kind = "restore",
            State = JournalState.Staged,
            Steps = [new JournalStep(JournalOp.MoveIn, path, "tracks", false)],
            KeepPrevious = true,
        })).Code.ShouldBe(LibraryJobErrorCodeDto.ArchiveUnsafe);
    }

    [Theory]
    [InlineData("tracks")]
    [InlineData("stemstudio.db-wal")]
    [InlineData("library-care/staging/1a2b3c4d-5e6f-4a7b-8c9d-0e1f2a3b4c5d/files/tracks")]
    [InlineData("library-care/previous/1a2b3c4d-5e6f-4a7b-8c9d-0e1f2a3b4c5d/stemstudio.db")]
    public void JournalPaths_InsideTheAllowedItemsPass(string path) => JournalPlans.IsAllowedPath(path).ShouldBeTrue();

    [Fact]
    public async Task Recovery_WithoutJournalsDoesNothing() => (await new RestoreJournal(_paths).RecoverAsync(_files, Ct)).ShouldBe(0);

    public void Dispose() => _root.Delete(recursive: true);

    private LibrarySwitcher Switcher() => new(_gate, new RestoreJournal(_paths), _files, _hook, TimeSpan.Zero);

    private string Path(string relative) => System.IO.Path.Combine(_paths.Data.Root, relative.Replace('/', System.IO.Path.DirectorySeparatorChar));

    private string Read(string relative) => File.ReadAllText(Path(relative));

    private void Write(string relative, string text)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path(relative))!);
        File.WriteAllText(Path(relative), text);
    }

    private sealed class CrashHook : IJournalStepHook
    {
        public int CrashAfter { get; set; } = -1;

        public bool MaintenanceSeen { get; private set; }

        public void AfterStep(int index, LibraryGate gate)
        {
            MaintenanceSeen |= gate.IsMaintenance;
            if (index == CrashAfter)
            {
                throw new SimulatedCrashException();
            }
        }
    }
}

/// <summary>Thrown by the crash hook.</summary>
internal sealed class SimulatedCrashException : Exception
{
}
