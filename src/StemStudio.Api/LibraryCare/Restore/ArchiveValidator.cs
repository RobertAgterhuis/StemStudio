using System.Globalization;
using System.IO.Compression;
using StemStudio.Api.LibraryCare.Archive;
using StemStudio.Api.LibraryCare.Backup;
using StemStudio.Api.LibraryCare.Jobs;
using StemStudio.Contracts.LibraryCare;
using StemStudio.Domain.LibraryCare;

namespace StemStudio.Api.LibraryCare.Restore;

/// <summary>An open, validated archive (steps 1-5 of data-model 3.4).</summary>
/// <param name="Zip">The archive.</param>
/// <param name="Manifest">Its manifest.</param>
public sealed record ValidatedArchive(ZipArchive Zip, BackupManifest Manifest) : IDisposable
{
    /// <inheritdoc />
    public void Dispose() => Zip.Dispose();
}

/// <summary>
/// Validates an archive before anything changes (data-model 3.4 steps 1-5): name and regular file, ZIP and manifest, every
/// entry, compatibility with this StemStudio's migrations, and free space on the data volume.
/// </summary>
/// <param name="paths">Library-care folders.</param>
/// <param name="files">Free-space seam.</param>
/// <param name="migrations">Known migrations.</param>
public sealed class ArchiveValidator(LibraryCarePaths paths, ILibraryFiles files, IKnownMigrations migrations)
{
    /// <summary>True when the file is a link (reparse point).</summary>
    /// <param name="file">The file.</param>
    /// <returns>True for a link.</returns>
    public static bool IsLink(FileInfo file)
    {
        ArgumentNullException.ThrowIfNull(file);
        return (file.Attributes & FileAttributes.ReparsePoint) != 0;
    }

    /// <summary>Runs steps 1-5.</summary>
    /// <param name="fileName">Archive name.</param>
    /// <returns>The open archive (caller disposes).</returns>
    public ValidatedArchive Validate(string fileName)
    {
        var file = new FileInfo(paths.Archive(ArchiveNames.IsValid(fileName) ? fileName : "-"));
        if (!ArchiveNames.IsValid(fileName) || !file.Exists || IsLink(file))
        {
            throw ArchiveReader.Invalid("The archive is not a regular file in the backups folder.");
        }

        var (zip, manifest) = ArchiveReader.Open(file.FullName);
        try
        {
            ArchiveReader.CheckEntries(zip, manifest);
            CheckCompatible(manifest);
            CheckSpace(manifest);
            return new ValidatedArchive(zip, manifest);
        }
        catch
        {
            zip.Dispose();
            throw;
        }
    }

    private void CheckCompatible(BackupManifest manifest)
    {
        if (manifest.Schema.Migrations.Except(migrations.All, StringComparer.Ordinal).Any())
        {
            throw new LibraryJobException(LibraryJobErrorCodeDto.ArchiveIncompatible, $"The backup was made by a newer StemStudio ({manifest.App.Version}); update StemStudio first.");
        }
    }

    private void CheckSpace(BackupManifest manifest)
    {
        var needed = manifest.Totals.Bytes + LibraryCareLimits.RestoreFreeMarginBytes;
        if (files.Space(paths.Data.Root) is { } space && space.FreeBytes < needed)
        {
            throw new LibraryJobException(
                LibraryJobErrorCodeDto.InsufficientDiskSpace,
                string.Create(CultureInfo.InvariantCulture, $"The restore needs about {needed / 1_000_000_000d:0.0} GB on the library disk; {space.FreeBytes / 1_000_000_000d:0.0} GB is free."));
        }
    }
}

/// <summary>
/// Extracts a validated archive into <c>library-care/staging/&lt;jobId&gt;/</c> (data-model 3.4 step 6): each destination is
/// <c>GetFullPath</c>-checked to stay inside staging, each entry is hashed while written and bounded by its declared size.
/// </summary>
/// <param name="limits">Library-care limits (throttle).</param>
/// <param name="clock">Clock of the optional throttle.</param>
public sealed class StagingExtractor(LibraryCareOptions limits, TimeProvider clock)
{
    /// <summary>The staging destination of an entry.</summary>
    /// <param name="stagingRoot">The staging folder.</param>
    /// <param name="entryName">The entry name.</param>
    /// <returns>The absolute path.</returns>
    /// <exception cref="LibraryJobException"><c>archive-unsafe</c> when it would leave staging.</exception>
    public static string Destination(string stagingRoot, string entryName)
    {
        ArgumentNullException.ThrowIfNull(entryName);
        var root = Path.GetFullPath(stagingRoot);
        var relative = entryName == LibraryLayout.DatabaseEntry ? "stemstudio.db" : entryName;
        var destination = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        return destination.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            ? destination
            : throw ArchiveReader.Unsafe("An entry would be written outside the restore folder; nothing was changed.");
    }

    /// <summary>Extracts every manifest entry.</summary>
    /// <param name="archive">The validated archive.</param>
    /// <param name="stagingRoot">The staging folder.</param>
    /// <param name="progress">Receives each extracted entry, or null.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    public async Task ExtractAsync(ValidatedArchive archive, string stagingRoot, IProgress<ManifestEntry>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(archive);
        foreach (var entry in archive.Manifest.Entries)
        {
            var destination = Destination(stagingRoot, entry.Path);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            await using (var file = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, limits.CopyBufferBytes, useAsync: true))
            {
                await ArchiveReader.CopyVerifiedAsync(archive.Zip, entry, ByteThrottle.Wrap(file, limits.ThrottleBytesPerSecond, clock), cancellationToken);
            }

            progress?.Report(entry);
        }
    }
}
