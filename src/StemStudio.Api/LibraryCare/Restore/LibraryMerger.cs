using System.Globalization;
using Microsoft.EntityFrameworkCore;
using StemStudio.Api.LibraryCare.Backup;
using StemStudio.Api.LibraryCare.Gate;
using StemStudio.Api.LibraryCare.Jobs;
using StemStudio.Api.Storage;
using StemStudio.Contracts.LibraryCare;
using StemStudio.Domain.LibraryCare;
using StemStudio.Persistence;

namespace StemStudio.Api.LibraryCare.Restore;

/// <summary>What a merge added and skipped.</summary>
/// <param name="Skipped">Skipped and renamed items.</param>
/// <param name="AddedIds">Ids of the added items in manifest form (<c>D</c>, lower case).</param>
public sealed record MergeResult(IReadOnlyList<SkippedItemDto> Skipped, IReadOnlySet<string> AddedIds);

/// <summary>
/// The merge restore (US5, research R-07, data-model 5.7): reads the staged database with a second context and inserts what
/// is missing into the live database in one transaction. The files of added items are copied from staging into their own
/// new folders before the commit (they are not visible without their rows); a failure before or at the commit removes
/// every copied file, so nothing of the merge remains. The commit runs under the maintenance gate. The library is never
/// moved aside and no previous library is kept.
/// </summary>
/// <param name="gate">The write gate.</param>
/// <param name="live">The live database.</param>
/// <param name="data">The data directory.</param>
/// <param name="files">File seam (copy failures are tested through it).</param>
public sealed class LibraryMerger(LibraryGate gate, LiveDatabase live, DataDirectory data, ILibraryFiles files)
{
    /// <summary>Error text of a failed merge.</summary>
    public const string FailedText = "The backup could not be added to your library; nothing of it was kept. Try again.";

    private static readonly TimeSpan IdleTimeout = TimeSpan.FromSeconds(10);

    /// <summary>Merges the staged library into the live one.</summary>
    /// <param name="stagingRoot">The staging folder (database checked, migrated and reconciled).</param>
    /// <param name="context">Progress.</param>
    /// <param name="cancellationToken">Cancellation (observed until the commit starts).</param>
    /// <returns>The result.</returns>
    public async Task<MergeResult> MergeAsync(string stagingRoot, LibraryJobContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        await using var staged = BackupScrubber.StagedContext(Path.Combine(stagingRoot, "stemstudio.db"));
        await using var target = new StemStudioDbContext(new DbContextOptionsBuilder<StemStudioDbContext>().UseSqlite(live.ConnectionString).Options);
        var plan = await new MergePlanner(staged, target).PlanAsync(cancellationToken);
        context.Report(new LibraryJobProgress(LibraryJobStageDto.Switch, "Adding the missing items to your library", 95, context.Job.BytesTotal, context.Job.BytesTotal, null));
        await new MergeCopies(files, stagingRoot, data.Root).CopyThenAsync(plan.Paths, () =>
        {
            context.MarkNotCancellable();
            return CommitAsync(target, plan);
        }, cancellationToken);
        return new MergeResult(plan.Skipped, plan.AddedIds.Select(id => id.ToString("D", CultureInfo.InvariantCulture)).ToHashSet(StringComparer.Ordinal));
    }

    private async Task CommitAsync(StemStudioDbContext target, MergePlan plan)
    {
        target.AddRange(plan.Added);
        gate.EnterMaintenance();
        try
        {
            await gate.WaitForIdleAsync(IdleTimeout, CancellationToken.None);
            await target.SaveChangesAsync(CancellationToken.None);
        }
        finally
        {
            gate.ExitMaintenance();
        }
    }
}

/// <summary>
/// The file copies of a merge: each data-relative folder or file of an added item is copied from <c>files/</c> in staging;
/// what did not exist before is remembered so a failed merge removes exactly that (an existing upload is kept as is).
/// </summary>
/// <param name="files">File seam.</param>
/// <param name="stagingRoot">The staging folder.</param>
/// <param name="dataRoot">The live data directory.</param>
public sealed class MergeCopies(ILibraryFiles files, string stagingRoot, string dataRoot)
{
    private readonly List<string> _created = [];

    /// <summary>Copies every path, then commits; any failure removes what was copied.</summary>
    /// <param name="paths">Data-relative folders and files.</param>
    /// <param name="commit">The database commit.</param>
    /// <param name="cancellationToken">Cancellation (observed between copies).</param>
    /// <returns>A task.</returns>
    /// <exception cref="LibraryJobException"><c>io-error</c> when a copy or the commit failed.</exception>
    public async Task CopyThenAsync(IEnumerable<string> paths, Func<Task> commit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(commit);
        try
        {
            CopyAll(paths, cancellationToken);
            await commit();
        }
        catch (Exception exception)
        {
            RollBack();
            if (exception is IOException or UnauthorizedAccessException or DbUpdateException)
            {
                throw new LibraryJobException(LibraryJobErrorCodeDto.IoError, LibraryMerger.FailedText, exception);
            }

            throw;
        }
    }

    private static void Delete(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
                return;
            }

            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Best effort: a leftover file has no row, is never shown and is listed by the storage overview.
        }
    }

    private void CopyAll(IEnumerable<string> paths, CancellationToken cancellationToken)
    {
        foreach (var path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CopyPath(path);
        }
    }

    private void CopyPath(string path)
    {
        var source = StagingExtractor.Destination(stagingRoot, LibraryLayout.ToEntryName(path));
        var target = Path.Combine(dataRoot, path.Replace('/', Path.DirectorySeparatorChar));
        if (Directory.Exists(source))
        {
            CopyFolder(source, target);
        }
        else if (File.Exists(source) && Remember(target, !File.Exists(target)))
        {
            files.Copy(source, target);
        }
    }

    private void CopyFolder(string source, string target)
    {
        var fresh = Remember(target, !Directory.Exists(target));
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var destination = Path.Combine(target, Path.GetRelativePath(source, file));
            Remember(destination, !fresh && !File.Exists(destination));
            files.Copy(file, destination);
        }
    }

    private bool Remember(string path, bool created)
    {
        if (created)
        {
            _created.Add(path);
        }

        return created;
    }

    private void RollBack() => Enumerable.Reverse(_created).ToList().ForEach(Delete);
}
