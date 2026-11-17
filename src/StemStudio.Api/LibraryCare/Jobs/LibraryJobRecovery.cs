using StemStudio.Contracts.LibraryCare;

namespace StemStudio.Api.LibraryCare.Jobs;

/// <summary>
/// Start-up recovery of library jobs (data-model 5.1): a queued or running record becomes failed <c>interrupted</c>, its
/// <c>&lt;archive&gt;.partial</c> and its staging folder are removed; staging folders of unknown jobs are removed too. The
/// restore journal is recovered before this runs (feature 013 T047).
/// </summary>
/// <param name="store">Job records.</param>
/// <param name="paths">Library-care folders.</param>
/// <param name="clock">Clock.</param>
public sealed class LibraryJobRecovery(LibraryJobStore store, LibraryCarePaths paths, TimeProvider clock)
{
    /// <summary>Recovers every interrupted job.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The number of jobs marked as interrupted.</returns>
    public async Task<int> RecoverAsync(CancellationToken cancellationToken)
    {
        var jobs = await store.ListAsync(cancellationToken);
        var interrupted = jobs.Where(job => !LibraryJobs.IsTerminal(job)).ToList();
        foreach (var job in interrupted)
        {
            await store.SaveAsync(LibraryJobs.Failed(job, LibraryJobErrorCodeDto.Interrupted, LibraryJobs.InterruptedText, clock.GetUtcNow()), cancellationToken);
            DeletePartial(job);
        }

        DeleteStaging();
        return interrupted.Count;
    }

    private void DeletePartial(LibraryJobDto job)
    {
        if (job.Kind == LibraryJobKindDto.Backup && job.ArchiveFileName is { } name && ArchiveNames.IsValid(name))
        {
            File.Delete(paths.Archive(name) + LibraryCareLimits.PartialSuffix);
        }
    }

    private void DeleteStaging()
    {
        if (Directory.Exists(paths.StagingDirectory))
        {
            foreach (var folder in Directory.EnumerateDirectories(paths.StagingDirectory))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }
}
