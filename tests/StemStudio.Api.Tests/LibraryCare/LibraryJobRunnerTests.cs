using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using StemStudio.Api.LibraryCare.Jobs;
using StemStudio.Api.Storage;
using StemStudio.Contracts.LibraryCare;

namespace StemStudio.Api.Tests.LibraryCare;

/// <summary>013 T019: one library job at a time, progress, ETA, cancellation and failure (data-model 5.1, FR-006).</summary>
public sealed class LibraryJobRunnerTests : IDisposable
{
    private static readonly VerifyReportDto Report = new() { Ok = true, EntriesChecked = 1, BytesChecked = 1, Problem = null };

    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("stemstudio-runner-");
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 11, 16, 14, 0, 0, TimeSpan.Zero));
    private readonly LibraryJobStore _store;
    private readonly LibraryJobRunner _runner;

    public LibraryJobRunnerTests()
    {
        var paths = new LibraryCarePaths(new DataDirectory(Path.Combine(_root.FullName, "data")), Path.Combine(_root.FullName, "backups"));
        _store = new LibraryJobStore(paths);
        _runner = new LibraryJobRunner(_store, _clock, NullLogger<LibraryJobRunner>.Instance);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Start_RunsTheWorkAndStoresTheCompletedJob()
    {
        _runner.TryStart(LibraryJobKindDto.Verify, "a.zip", (_, _) => Task.FromResult<LibraryJobReportDto>(Report), out var queued).ShouldBeTrue();
        (queued.Status, queued.Kind, queued.ArchiveFileName, queued.Cancellable).ShouldBe((LibraryJobStatusDto.Queued, LibraryJobKindDto.Verify, "a.zip", true));

        await _runner.WhenIdleAsync();

        var job = (await _runner.GetAsync(queued.JobId, Ct))!;
        (job.Status, job.Progress, job.Cancellable, job.Stage, job.EtaSeconds).ShouldBe((LibraryJobStatusDto.Completed, 100, false, null, null));
        job.Report.ShouldBe(Report);
        (job.StartedAt, job.FinishedAt).ShouldBe((_clock.GetUtcNow(), _clock.GetUtcNow()));
        (await _store.FindAsync(queued.JobId, Ct))!.Status.ShouldBe(LibraryJobStatusDto.Completed);
        _runner.Active.ShouldBeNull();
    }

    [Fact]
    public async Task Hooks_BeginBeforeTheJobIsVisible_EndAfterTheFinishedJobIsStored()
    {
        // 013 write gate (E2E library-care-backup:159): the gate must be closed whenever the backup job reads as active.
        var release = new TaskCompletionSource();
        var events = new List<string>();
        Guid? id = null;
        var hooks = new LibraryJobHooks(
            () => events.Add("begin"),
            () => events.Add($"end:{_store.FindAsync(id!.Value, CancellationToken.None).GetAwaiter().GetResult()?.Status}"));

        _runner.TryStart(new LibraryJobStart(LibraryJobKindDto.Backup, "a.zip", async (_, ct) => { await release.Task.WaitAsync(ct); return Report; }, hooks), out var queued).ShouldBeTrue();
        id = queued.JobId;

        events.ShouldBe(["begin"]);
        release.SetResult();
        await _runner.WhenIdleAsync();
        events.ShouldBe(["begin", $"end:{LibraryJobStatusDto.Completed}"]);
    }

    [Fact]
    public void Hooks_ARefusedStart_RunsNeither()
    {
        var release = new TaskCompletionSource();
        var events = new List<string>();
        _runner.TryStart(LibraryJobKindDto.Verify, "a.zip", async (_, ct) => { await release.Task.WaitAsync(ct); return Report; }, out _).ShouldBeTrue();

        _runner.TryStart(new LibraryJobStart(LibraryJobKindDto.Backup, "b.zip", (_, _) => Task.FromResult<LibraryJobReportDto>(Report), new LibraryJobHooks(() => events.Add("begin"), () => events.Add("end"))), out _).ShouldBeFalse();

        events.ShouldBeEmpty();
        release.SetResult();
    }

    [Fact]
    public async Task Start_OnlyOneJobAtATime()
    {
        var release = new TaskCompletionSource();
        _runner.TryStart(LibraryJobKindDto.Backup, "a.zip", async (_, ct) => { await release.Task.WaitAsync(ct); return Report; }, out var first).ShouldBeTrue();

        _runner.TryStart(LibraryJobKindDto.Verify, "b.zip", (_, _) => Task.FromResult<LibraryJobReportDto>(Report), out _).ShouldBeFalse();
        _runner.Active!.JobId.ShouldBe(first.JobId);
        _runner.IsArchiveInUse("a.zip").ShouldBeTrue();
        _runner.IsArchiveInUse("b.zip").ShouldBeFalse();

        release.SetResult();
        await _runner.WhenIdleAsync();
        _runner.IsArchiveInUse("a.zip").ShouldBeFalse();
        _runner.TryStart(LibraryJobKindDto.Verify, "b.zip", (_, _) => Task.FromResult<LibraryJobReportDto>(Report), out _).ShouldBeTrue();
        await _runner.WhenIdleAsync();
    }

    [Fact]
    public async Task Cancel_PropagatesTheTokenAndEndsCancelled()
    {
        var started = new TaskCompletionSource();
        var observed = false;
        _runner.TryStart(LibraryJobKindDto.Backup, "a.zip", async (_, ct) =>
        {
            started.SetResult();
            try
            {
                await Task.Delay(Timeout.Infinite, ct);
            }
            catch (OperationCanceledException)
            {
                observed = true;
                throw;
            }

            return Report;
        }, out var job).ShouldBeTrue();
        await started.Task;

        var (result, cancelling) = await _runner.CancelAsync(job.JobId, Ct);
        await _runner.WhenIdleAsync();

        (result, cancelling!.JobId).ShouldBe((CancelResult.Requested, job.JobId));
        observed.ShouldBeTrue();
        var cancelled = (await _runner.GetAsync(job.JobId, Ct))!;
        (cancelled.Status, cancelled.Cancellable, cancelled.Error).ShouldBe((LibraryJobStatusDto.Cancelled, false, null));
    }

    [Fact]
    public async Task Cancel_RefusedOnceTheJobIsNotCancellableOrFinished()
    {
        var release = new TaskCompletionSource();
        var marked = new TaskCompletionSource();
        _runner.TryStart(LibraryJobKindDto.Restore, "a.zip", async (context, _) =>
        {
            context.MarkNotCancellable();
            marked.SetResult();
            await release.Task;
            return Report;
        }, out var job).ShouldBeTrue();
        await marked.Task;

        (await _runner.CancelAsync(job.JobId, Ct)).Result.ShouldBe(CancelResult.NotCancellable);
        release.SetResult();
        await _runner.WhenIdleAsync();
        (await _runner.CancelAsync(job.JobId, Ct)).Result.ShouldBe(CancelResult.NotCancellable);
        (await _runner.CancelAsync(Guid.NewGuid(), Ct)).Result.ShouldBe(CancelResult.NotFound);
    }

    [Fact]
    public async Task Cancel_OfAStoredTerminalJobIsNotCancellable()
    {
        var stored = LibraryJobs.Queued(Guid.NewGuid(), LibraryJobKindDto.Backup, "a.zip", _clock.GetUtcNow()) with { Status = LibraryJobStatusDto.Completed };
        await _store.SaveAsync(stored, Ct);

        (await _runner.CancelAsync(stored.JobId, Ct)).Result.ShouldBe(CancelResult.NotCancellable);
    }

    [Fact]
    public async Task Failure_WithALibraryJobErrorKeepsItsCode()
    {
        _runner.TryStart(LibraryJobKindDto.Restore, "a.zip", (_, _) => throw new LibraryJobException(LibraryJobErrorCodeDto.ArchiveUnsafe, "Entry is outside the layout."), out var job).ShouldBeTrue();
        await _runner.WhenIdleAsync();

        var failed = (await _runner.GetAsync(job.JobId, Ct))!;
        (failed.Status, failed.Error!.Code, failed.Error.Message, failed.Report).ShouldBe(
            (LibraryJobStatusDto.Failed, LibraryJobErrorCodeDto.ArchiveUnsafe, "Entry is outside the layout.", null));
    }

    [Fact]
    public async Task Failure_UnexpectedBecomesAnIoErrorWithoutTheExceptionText()
    {
        _runner.TryStart(LibraryJobKindDto.Backup, "a.zip", (_, _) => throw new IOException(@"C:\secret\path is locked"), out var job).ShouldBeTrue();
        await _runner.WhenIdleAsync();

        var failed = (await _runner.GetAsync(job.JobId, Ct))!;
        (failed.Status, failed.Error!.Code).ShouldBe((LibraryJobStatusDto.Failed, LibraryJobErrorCodeDto.IoError));
        failed.Error.Message.ShouldNotContain("secret");
    }

    [Fact]
    public async Task Progress_IsVisibleWithAnEtaAfterFiveSeconds()
    {
        var step = new SemaphoreSlim(0);
        var next = new SemaphoreSlim(0);
        _runner.TryStart(LibraryJobKindDto.Backup, "a.zip", async (context, ct) =>
        {
            context.Report(new LibraryJobProgress(LibraryJobStageDto.Files, "Writing stems: 1 of 4", 10, 100, 1000, "Song - stems"));
            next.Release();
            await step.WaitAsync(ct);
            context.Report(new LibraryJobProgress(LibraryJobStageDto.Files, "Writing stems: 2 of 4", 30, 300, 1000, "Song - stems"));
            next.Release();
            await step.WaitAsync(ct);
            return Report;
        }, out var job).ShouldBeTrue();

        await next.WaitAsync(Ct);
        var first = _runner.Active!;
        (first.Status, first.Stage, first.StageText, first.Progress, first.BytesDone, first.CurrentItem, first.EtaSeconds).ShouldBe(
            (LibraryJobStatusDto.Running, LibraryJobStageDto.Files, "Writing stems: 1 of 4", 10, 100L, "Song - stems", null));
        _clock.Advance(TimeSpan.FromSeconds(6));
        step.Release();
        await next.WaitAsync(Ct);

        _runner.Active!.EtaSeconds.ShouldBe(21);
        step.Release();
        await _runner.WhenIdleAsync();
        (await _runner.GetAsync(job.JobId, Ct))!.BytesDone.ShouldBe(300);
    }

    [Fact]
    public async Task List_IncludesTheActiveJobAndHonoursTheLimit()
    {
        for (var i = 0; i < 3; i++)
        {
            _runner.TryStart(LibraryJobKindDto.Verify, "a.zip", (_, _) => Task.FromResult<LibraryJobReportDto>(Report), out _).ShouldBeTrue();
            await _runner.WhenIdleAsync();
            _clock.Advance(TimeSpan.FromMinutes(1));
        }

        (await _runner.ListAsync(2, Ct)).Count.ShouldBe(2);
        (await _runner.ListAsync(10, Ct)).Count.ShouldBe(3);
    }

    public void Dispose() => _root.Delete(recursive: true);
}
