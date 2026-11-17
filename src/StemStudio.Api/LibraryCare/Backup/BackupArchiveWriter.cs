using System.Globalization;
using StemStudio.Api.LibraryCare.Archive;
using StemStudio.Api.LibraryCare.Jobs;
using StemStudio.Contracts.LibraryCare;
using StemStudio.Domain.LibraryCare;

namespace StemStudio.Api.LibraryCare.Backup;

/// <summary>What the files stage writes.</summary>
/// <param name="PartialPath">The <c>.partial</c> archive.</param>
/// <param name="DatabasePath">The scrubbed database copy.</param>
/// <param name="Plan">The selected files.</param>
/// <param name="Warnings">Warnings so far; missing and changed files are added.</param>
public sealed record ArchiveJob(string PartialPath, string DatabasePath, BackupPlan Plan, List<ArchiveWarningDto> Warnings);

/// <summary>
/// The files, manifest and README stages of a backup: copies the database and every selected file with progress
/// (96 % of the job by bytes), stops below the free-space floor of the backups folder, records missing and changed files.
/// </summary>
/// <param name="files">Free-space seam.</param>
/// <param name="limits">Library-care limits.</param>
/// <param name="backupsDirectory">The backups folder.</param>
public sealed class BackupArchiveWriter(ILibraryFiles files, LibraryCareOptions limits, string backupsDirectory) : IAsyncDisposable
{
    private ArchiveWriter? _writer;

    /// <summary>Writes the database and the files.</summary>
    /// <param name="job">The job.</param>
    /// <param name="context">Progress.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The written entries in archive order.</returns>
    public async Task<IReadOnlyList<ManifestEntry>> WriteAsync(ArchiveJob job, LibraryJobContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(context);
        _writer = ArchiveWriter.Create(job.PartialPath, limits.CopyBufferBytes);
        var sources = new List<SelectedFile> { new(new ArchiveSource(LibraryLayout.DatabaseEntry, BackupCategory.Database, job.DatabasePath, null), "Database") };
        sources.AddRange(job.Plan.Files);
        var progress = new FilesProgress(context, sources);
        var entries = new List<ManifestEntry>();
        foreach (var file in sources)
        {
            if (await WriteFileAsync(file, progress, job.Warnings, cancellationToken) is { } entry)
            {
                entries.Add(entry);
            }
        }

        return entries;
    }

    /// <summary>Writes the README and the manifest (last) and closes the archive.</summary>
    /// <param name="manifest">The manifest.</param>
    /// <param name="context">Progress.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    public async Task FinishAsync(BackupManifest manifest, LibraryJobContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var writer = _writer ?? throw new InvalidOperationException("WriteAsync was not called.");
        context.Report(new LibraryJobProgress(LibraryJobStageDto.Manifest, "Writing the manifest", 98, context.Job.BytesTotal, context.Job.BytesTotal, null));
        await writer.AddTextAsync(ArchiveWriter.ReadmeEntry, ArchiveWriter.ReadmeText, cancellationToken);
        await writer.AddManifestAsync(manifest, cancellationToken);
        await DisposeAsync();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_writer is { } writer)
        {
            _writer = null;
            await writer.DisposeAsync();
        }
    }

    private async Task<ManifestEntry?> WriteFileAsync(SelectedFile file, FilesProgress progress, List<ArchiveWarningDto> warnings, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureFreeSpace(progress.SizeOf(file));
        progress.Start(file);
        var written = await _writer!.AddFileCheckedAsync(file.Source, progress, cancellationToken);
        if (written is null)
        {
            warnings.Add(Warning(ArchiveWarningCodeDto.MissingFile, $"{file.Label}: a file was missing and is not in the backup."));
            return null;
        }

        if (written.Changed)
        {
            warnings.Add(Warning(ArchiveWarningCodeDto.ChangedAfterSnapshot, $"{file.Label}: a file changed while it was copied; the archive has the copied version."));
        }

        return written.Entry;
    }

    private void EnsureFreeSpace(long next)
    {
        if (files.Space(backupsDirectory) is { } space && space.FreeBytes - next < limits.StopBelowFreeBytes)
        {
            throw new LibraryJobException(LibraryJobErrorCodeDto.InsufficientDiskSpace, "The backups folder is almost full; the backup stopped and nothing was kept.");
        }
    }

    private static ArchiveWarningDto Warning(ArchiveWarningCodeDto code, string message) => new() { Code = code, Message = message.Length > 500 ? message[..500] : message };

    /// <summary>Progress of the files stage: 2 % before, 96 % by bytes, stage text per category.</summary>
    private sealed class FilesProgress : IProgress<long>
    {
        private readonly LibraryJobContext _context;
        private readonly Dictionary<string, long> _sizes;
        private readonly Dictionary<BackupCategory, int> _totals;
        private readonly Dictionary<BackupCategory, int> _done = [];
        private readonly long _total;
        private long _bytes;
        private SelectedFile? _current;

        public FilesProgress(LibraryJobContext context, IReadOnlyList<SelectedFile> sources)
        {
            _context = context;
            _sizes = sources.ToDictionary(source => source.Source.EntryName, source => Size(source.Source.SourcePath), StringComparer.Ordinal);
            _totals = sources.GroupBy(source => source.Source.Category).ToDictionary(group => group.Key, group => group.Count());
            _total = Math.Max(1, _sizes.Values.Sum());
        }

        public long SizeOf(SelectedFile file) => _sizes[file.Source.EntryName];

        public void Start(SelectedFile file)
        {
            _current = file;
            var category = file.Source.Category;
            _done[category] = _done.GetValueOrDefault(category) + 1;
            Publish();
        }

        public void Report(long value)
        {
            _bytes += value;
            Publish();
        }

        private static long Size(string path) => new FileInfo(path) is { Exists: true } info ? info.Length : 0;

        private void Publish()
        {
            var category = _current!.Source.Category;
            var text = string.Create(CultureInfo.InvariantCulture, $"Writing {CategoryNames.Of(category)}: {_done[category]} of {_totals[category]}");
            var percent = 2 + (int)(96 * Math.Min(_bytes, _total) / _total);
            _context.Report(new LibraryJobProgress(LibraryJobStageDto.Files, text, percent, _bytes, _total, _current.Label));
        }
    }
}

/// <summary>Readable names of the archive categories for stage texts.</summary>
public static class CategoryNames
{
    /// <summary>The name, e.g. "detail stems".</summary>
    /// <param name="category">The category.</param>
    /// <returns>The name.</returns>
    public static string Of(BackupCategory category) => category switch
    {
        BackupCategory.PendingUploads => "pending uploads",
        BackupCategory.DetailStems => "detail stems",
        BackupCategory.Scores => "sheet music",
        BackupCategory.VoiceProfiles => "voice samples",
        _ => category.ToString().ToLowerInvariant(),
    };
}
