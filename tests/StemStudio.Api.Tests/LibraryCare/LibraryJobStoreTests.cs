using Microsoft.Extensions.Time.Testing;
using StemStudio.Api.LibraryCare.Jobs;
using StemStudio.Api.Storage;
using StemStudio.Contracts.LibraryCare;

namespace StemStudio.Api.Tests.LibraryCare;

/// <summary>013 T019: library job records (data-model 5.1): JSON files, last 50 kept, start-up recovery (FR-006).</summary>
public sealed class LibraryJobStoreTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("stemstudio-jobs-");
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 11, 16, 14, 0, 0, TimeSpan.Zero));
    private readonly LibraryCarePaths _paths;
    private readonly LibraryJobStore _store;

    public LibraryJobStoreTests()
    {
        _paths = new LibraryCarePaths(new DataDirectory(Path.Combine(_root.FullName, "data")), Path.Combine(_root.FullName, "backups"));
        _store = new LibraryJobStore(_paths);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Save_WritesOneJsonRecordPerJobAndReadsItBack()
    {
        var job = Job(LibraryJobStatusDto.Completed) with { Report = new VerifyReportDto { Ok = true, EntriesChecked = 3, BytesChecked = 10, Problem = null } };

        await _store.SaveAsync(job, Ct);

        File.Exists(Path.Combine(_paths.JobsDirectory, $"{job.JobId:D}.json")).ShouldBeTrue();
        var read = (await _store.FindAsync(job.JobId, Ct)).ShouldNotBeNull();
        read.Report.ShouldBeOfType<VerifyReportDto>().EntriesChecked.ShouldBe(3);
        (await File.ReadAllTextAsync(Path.Combine(_paths.JobsDirectory, $"{job.JobId:D}.json"), Ct)).ShouldContain("\"type\":\"verify\"");
    }

    [Fact]
    public async Task Find_UnknownOrUnreadableIsNull()
    {
        (await _store.FindAsync(Guid.NewGuid(), Ct)).ShouldBeNull();
        Directory.CreateDirectory(_paths.JobsDirectory);
        var id = Guid.NewGuid();
        await File.WriteAllTextAsync(Path.Combine(_paths.JobsDirectory, $"{id:D}.json"), "{nope", Ct);

        (await _store.FindAsync(id, Ct)).ShouldBeNull();
        (await _store.ListAsync(Ct)).ShouldBeEmpty();
    }

    [Fact]
    public async Task List_IsNewestFirstAndKeepsTheLast50()
    {
        var ids = new List<Guid>();
        for (var i = 0; i < 52; i++)
        {
            var job = Job(LibraryJobStatusDto.Completed) with { CreatedAt = _clock.GetUtcNow().AddMinutes(i) };
            ids.Add(job.JobId);
            await _store.SaveAsync(job, Ct);
        }

        var list = await _store.ListAsync(Ct);

        list.Count.ShouldBe(50);
        list[0].JobId.ShouldBe(ids[^1]);
        list.Select(job => job.JobId).ShouldNotContain(ids[0]);
        Directory.GetFiles(_paths.JobsDirectory, "*.json").Length.ShouldBe(50);
    }

    [Fact]
    public async Task Recovery_FailsRunningJobsAsInterruptedAndRemovesPartialAndStaging()
    {
        var backup = Job(LibraryJobStatusDto.Running) with { ArchiveFileName = "stemstudio-backup-20261116-140000.zip" };
        var restore = Job(LibraryJobStatusDto.Queued) with { Kind = LibraryJobKindDto.Restore, ArchiveFileName = "x.zip" };
        var done = Job(LibraryJobStatusDto.Completed);
        foreach (var job in new[] { backup, restore, done })
        {
            await _store.SaveAsync(job, Ct);
        }

        Directory.CreateDirectory(_paths.BackupsDirectory);
        var partial = Path.Combine(_paths.BackupsDirectory, backup.ArchiveFileName + ".partial");
        await File.WriteAllTextAsync(partial, "x", Ct);
        var staging = _paths.Staging(restore.JobId);
        Directory.CreateDirectory(Path.Combine(staging, "files"));
        var foreignArchive = Path.Combine(_paths.BackupsDirectory, "x.zip");
        await File.WriteAllTextAsync(foreignArchive, "keep", Ct);

        var recovered = await new LibraryJobRecovery(_store, _paths, _clock).RecoverAsync(Ct);

        recovered.ShouldBe(2);
        var interrupted = (await _store.FindAsync(backup.JobId, Ct))!;
        (interrupted.Status, interrupted.Error!.Code, interrupted.Cancellable, interrupted.FinishedAt).ShouldBe(
            (LibraryJobStatusDto.Failed, LibraryJobErrorCodeDto.Interrupted, false, _clock.GetUtcNow()));
        (await _store.FindAsync(restore.JobId, Ct))!.Status.ShouldBe(LibraryJobStatusDto.Failed);
        (await _store.FindAsync(done.JobId, Ct))!.Status.ShouldBe(LibraryJobStatusDto.Completed);
        File.Exists(partial).ShouldBeFalse();
        Directory.Exists(staging).ShouldBeFalse();
        File.Exists(foreignArchive).ShouldBeTrue();
    }

    [Fact]
    public async Task Recovery_RemovesStagingFoldersOfUnknownJobs()
    {
        var orphan = _paths.Staging(Guid.NewGuid());
        Directory.CreateDirectory(orphan);

        (await new LibraryJobRecovery(_store, _paths, _clock).RecoverAsync(Ct)).ShouldBe(0);

        Directory.Exists(orphan).ShouldBeFalse();
    }

    [Fact]
    public void Paths_AreInsideTheDataDirectory()
    {
        var id = Guid.NewGuid();

        _paths.Staging(id).ShouldStartWith(Path.Combine(_paths.Data.Root, "library-care", "staging"));
        _paths.Previous(id).ShouldStartWith(Path.Combine(_paths.Data.Root, "library-care", "previous"));
        _paths.Journal(id).ShouldEndWith($"{id:D}.json");
        _paths.CleanupDirectory.ShouldEndWith("cleanup");
        _paths.Archive("a.zip").ShouldBe(Path.Combine(_paths.BackupsDirectory, "a.zip"));
    }

    public void Dispose() => _root.Delete(recursive: true);

    private LibraryJobDto Job(LibraryJobStatusDto status) => LibraryJobs.Queued(Guid.NewGuid(), LibraryJobKindDto.Backup, "a.zip", _clock.GetUtcNow()) with
    {
        Status = status,
    };
}
