using System.IO.Compression;
using System.Text;
using System.Text.Json;
using StemStudio.Domain.LibraryCare;

namespace StemStudio.Api.LibraryCare.Archive;

/// <summary>A file to write into the archive.</summary>
/// <param name="EntryName">Entry name (<c>db/stemstudio.db</c> or <c>files/</c> + a layout path).</param>
/// <param name="Category">Category.</param>
/// <param name="SourcePath">Absolute source path.</param>
/// <param name="Entity">Owning entity, if any.</param>
public sealed record ArchiveSource(string EntryName, BackupCategory Category, string SourcePath, ManifestEntity? Entity);

/// <summary>The outcome of one copied file.</summary>
/// <param name="Entry">The manifest entry.</param>
/// <param name="Changed">The size changed while it was copied.</param>
public sealed record WrittenEntry(ManifestEntry Entry, bool Changed);

/// <summary>
/// Writes a backup archive (backup-archive 1.0.0): one ZIP on a seekable <c>.partial</c> file (Zip64 written by
/// <see cref="ZipArchive"/> when needed), audio, pictures and PDFs stored, everything else deflated (fastest), each file
/// hashed while it is copied.
/// </summary>
public sealed class ArchiveWriter : IAsyncDisposable
{
    /// <summary>Entry name of the README.</summary>
    public const string ReadmeEntry = LibraryLayout.ReadmeEntry;

    /// <summary>Text of <c>README.txt</c>.</summary>
    public const string ReadmeText = """
        StemStudio library backup
        =========================

        This ZIP file is a complete backup of a StemStudio library: the database (db/stemstudio.db), the stems and the
        other library files (files/...) and a manifest (manifest.json) with a SHA-256 hash of every entry.

        To restore it, put the file in the StemStudio backups folder and choose "Restore" on the Library care page.
        Do not change or repack this file: StemStudio checks every entry before it restores anything.

        This archive is not encrypted. It may contain voice samples (personal data); keep it on an encrypted drive.
        """;

    private static readonly HashSet<string> StoredExtensions = new(StringComparer.OrdinalIgnoreCase) { ".flac", ".wav", ".mp3", ".jpg", ".png", ".pdf", ".webm" };

    private readonly FileStream _file;
    private readonly ZipArchive _zip;
    private readonly int _bufferSize;

    private ArchiveWriter(FileStream file, int bufferSize)
    {
        (_file, _bufferSize) = (file, bufferSize);
        _zip = new ZipArchive(file, ZipArchiveMode.Create, leaveOpen: true, Encoding.UTF8);
    }

    /// <summary>Creates the archive file (replacing an old partial file).</summary>
    /// <param name="partialPath">The <c>.partial</c> path.</param>
    /// <param name="bufferSize">Copy buffer size.</param>
    /// <returns>The writer.</returns>
    public static ArchiveWriter Create(string partialPath, int bufferSize) =>
        new(new FileStream(partialPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None, bufferSize, FileOptions.Asynchronous), bufferSize);

    /// <summary>True when an entry with this name is stored without compression.</summary>
    /// <param name="entryName">The entry name.</param>
    /// <returns>True for audio, pictures and PDFs.</returns>
    public static bool IsStored(string entryName) => StoredExtensions.Contains(Path.GetExtension(entryName));

    /// <summary>Copies a file into the archive, hashing it.</summary>
    /// <param name="source">The file.</param>
    /// <param name="progress">Receives the bytes copied, or null.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The manifest entry, or null when the file does not exist (nothing is written).</returns>
    public async Task<ManifestEntry?> AddFileAsync(ArchiveSource source, IProgress<long>? progress, CancellationToken cancellationToken) =>
        (await AddFileCheckedAsync(source, progress, cancellationToken))?.Entry;

    /// <summary>Copies a file into the archive, hashing it, and tells whether its size changed while copied.</summary>
    /// <param name="source">The file.</param>
    /// <param name="progress">Receives the bytes copied, or null.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The outcome, or null when the file does not exist.</returns>
    public async Task<WrittenEntry?> AddFileCheckedAsync(ArchiveSource source, IProgress<long>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        FileStream input;
        try
        {
            input = new FileStream(source.SourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, _bufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }

        await using (input)
        {
            var before = input.Length;
            var (size, hash) = await CopyAsync(input, source.EntryName, progress, cancellationToken);
            return new WrittenEntry(new ManifestEntry(source.EntryName, source.Category, size, hash, source.Entity), size != before);
        }
    }

    /// <summary>Writes a UTF-8 text entry (deflated).</summary>
    /// <param name="entryName">Entry name.</param>
    /// <param name="text">The text.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    public async Task AddTextAsync(string entryName, string text, CancellationToken cancellationToken)
    {
        await using var entry = _zip.CreateEntry(entryName, CompressionLevel.Fastest).Open();
        await entry.WriteAsync(Encoding.UTF8.GetBytes(text), cancellationToken);
    }

    /// <summary>Writes <c>manifest.json</c> (must be the last entry).</summary>
    /// <param name="manifest">The manifest.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    public async Task AddManifestAsync(BackupManifest manifest, CancellationToken cancellationToken)
    {
        await using var entry = _zip.CreateEntry(LibraryLayout.ManifestEntry, CompressionLevel.Fastest).Open();
        await JsonSerializer.SerializeAsync(entry, manifest, ManifestJson.Options, cancellationToken);
    }

    /// <summary>
    /// Post-write check (writer rule 3): the central directory lists exactly the manifest's entries (names and sizes) plus
    /// the README and the manifest.
    /// </summary>
    /// <param name="archivePath">The written archive.</param>
    /// <param name="manifest">The manifest that was written.</param>
    /// <returns>True when they match.</returns>
    public static bool MatchesManifest(string archivePath, BackupManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        try
        {
            using var zip = ZipFile.OpenRead(archivePath);
            var expected = manifest.Entries.Select(entry => (entry.Path, entry.SizeBytes))
                .Concat(zip.Entries.Where(IsBookkeeping).Select(entry => (entry.FullName, entry.Length)));
            return zip.Entries.Count == manifest.Entries.Count + 2
                && zip.Entries.Select(entry => (entry.FullName, entry.Length)).SequenceEqual(expected);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Renames the finished <c>.partial</c> file to its final name.</summary>
    /// <param name="partialPath">The partial file.</param>
    /// <param name="finalPath">The archive name.</param>
    public static void Commit(string partialPath, string finalPath) => File.Move(partialPath, finalPath, overwrite: false);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _zip.Dispose();
        await _file.FlushAsync();
        _file.Flush(flushToDisk: true);
        await _file.DisposeAsync();
    }

    private static bool IsBookkeeping(ZipArchiveEntry entry) => entry.FullName is LibraryLayout.ReadmeEntry or LibraryLayout.ManifestEntry;

    private async Task<(long Size, string Hash)> CopyAsync(FileStream input, string entryName, IProgress<long>? progress, CancellationToken cancellationToken)
    {
        var level = IsStored(entryName) ? CompressionLevel.NoCompression : CompressionLevel.Fastest;
        await using var entry = _zip.CreateEntry(entryName, level).Open();
        await using var hashing = new HashingLimitedStream(entry, long.MaxValue);
        var buffer = new byte[_bufferSize];
        int read;
        while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await hashing.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            progress?.Report(read);
        }

        return (hashing.Count, hashing.Sha256());
    }
}
