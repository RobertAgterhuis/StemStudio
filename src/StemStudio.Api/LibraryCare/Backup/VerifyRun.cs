using System.Globalization;
using System.IO.Compression;
using Microsoft.Data.Sqlite;
using StemStudio.Api.LibraryCare.Archive;
using StemStudio.Api.LibraryCare.Jobs;
using StemStudio.Contracts.LibraryCare;

namespace StemStudio.Api.LibraryCare.Backup;

/// <summary>
/// The work of a verify job: structure, names, links, the manifest's entry list, every SHA-256 and the database integrity,
/// without restoring anything. A damaged archive completes the job with <c>ok: false</c> and the first problem.
/// </summary>
/// <param name="paths">Library-care folders.</param>
/// <param name="options">API options (throttle).</param>
/// <param name="clock">Clock of the optional throttle.</param>
public sealed class VerifyRun(LibraryCarePaths paths, Microsoft.Extensions.Options.IOptions<StemStudioApiOptions> options, TimeProvider clock)
{
    /// <summary>Verifies an archive.</summary>
    /// <param name="fileName">Archive name.</param>
    /// <param name="context">Progress.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The verify report.</returns>
    public async Task<LibraryJobReportDto> RunAsync(string fileName, LibraryJobContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var progress = new VerifyProgress(context);
        var staging = paths.Staging(context.JobId);
        try
        {
            context.Report(new LibraryJobProgress(LibraryJobStageDto.Validate, "Checking the archive", 0, 0, 0, null));
            var (zip, manifest) = ArchiveReader.Open(paths.Archive(fileName));
            using (zip)
            {
                ArchiveReader.CheckEntries(zip, manifest);
                await VerifyEntriesAsync((zip, manifest), progress, staging, cancellationToken);
            }

            return new VerifyReportDto { Ok = true, EntriesChecked = progress.Entries, BytesChecked = progress.Bytes, Problem = null };
        }
        catch (LibraryJobException exception) when (IsArchiveProblem(exception.Code))
        {
            return new VerifyReportDto { Ok = false, EntriesChecked = progress.Entries, BytesChecked = progress.Bytes, Problem = new LibraryJobErrorDto { Code = exception.Code, Message = exception.Message } };
        }
        finally
        {
            if (Directory.Exists(staging))
            {
                Directory.Delete(staging, recursive: true);
            }
        }
    }

    private static bool IsArchiveProblem(LibraryJobErrorCodeDto code) =>
        code is LibraryJobErrorCodeDto.ArchiveCorrupt or LibraryJobErrorCodeDto.ArchiveInvalid or LibraryJobErrorCodeDto.ArchiveUnsafe or LibraryJobErrorCodeDto.ArchiveIncompatible;

    private async Task VerifyEntriesAsync((ZipArchive Zip, BackupManifest Manifest) archive, VerifyProgress progress, string staging, CancellationToken cancellationToken)
    {
        var (zip, manifest) = archive;
        progress.Begin(manifest);
        foreach (var entry in manifest.Entries)
        {
            if (entry.Path == Domain.LibraryCare.LibraryLayout.DatabaseEntry)
            {
                await VerifyDatabaseAsync(zip, entry, staging, cancellationToken);
            }
            else
            {
                await ArchiveReader.CopyVerifiedAsync(zip, entry, ByteThrottle.Wrap(Stream.Null, options.Value.LibraryCare.ThrottleBytesPerSecond, clock), cancellationToken);
            }

            progress.Done(entry);
        }
    }

    private static async Task VerifyDatabaseAsync(ZipArchive zip, ManifestEntry entry, string staging, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(staging);
        var path = Path.Combine(staging, "stemstudio.db");
        await using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        {
            await ArchiveReader.CopyVerifiedAsync(zip, entry, file, cancellationToken);
        }

        await using var connection = new SqliteConnection(DatabaseSnapshot.StagingConnectionString(path, readOnly: true));
        try
        {
            await connection.OpenAsync(cancellationToken);
            if (await DatabaseSnapshot.IsIntactAsync(connection, cancellationToken))
            {
                return;
            }
        }
        catch (SqliteException)
        {
            // Not a database: reported below.
        }

        throw ArchiveReader.Corrupt("The database in the archive is damaged; nothing was changed.");
    }

    private sealed class VerifyProgress(LibraryJobContext context)
    {
        private long _total = 1;
        private int _count;

        public int Entries { get; private set; }

        public long Bytes { get; private set; }

        public void Begin(BackupManifest manifest) => (_total, _count) = (Math.Max(1, manifest.Totals.Bytes), manifest.Entries.Count);

        public void Done(ManifestEntry entry)
        {
            (Entries, Bytes) = (Entries + 1, Bytes + entry.SizeBytes);
            var text = string.Create(CultureInfo.InvariantCulture, $"Unpacking and verifying {Entries} of {_count}");
            context.Report(new LibraryJobProgress(LibraryJobStageDto.Extract, text, 1 + (int)(99 * Bytes / _total), Bytes, _total, null));
        }
    }
}
