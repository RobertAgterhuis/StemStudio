using System.IO.Compression;
using System.Text.Json;
using StemStudio.Api.LibraryCare.Jobs;
using StemStudio.Contracts.LibraryCare;
using StemStudio.Domain.LibraryCare;

namespace StemStudio.Api.LibraryCare.Archive;

/// <summary>
/// Reads an archive as untrusted input (data-model 3.4 steps 2 and 3, research R-06): central directory, limits, the
/// manifest, entry names, links, duplicates and the entry list against the manifest; hashes are checked while reading.
/// Failures throw <see cref="LibraryJobException"/> with <c>archive-invalid</c>, <c>archive-unsafe</c> or <c>archive-corrupt</c>.
/// </summary>
public static class ArchiveReader
{
    private const int UnixLinkMode = 0xA000;
    private const int UnixTypeMask = 0xF000;
    private const string TooManyEntries = "The archive has more entries than a StemStudio backup may have.";
    private const string ManifestTooLarge = "The manifest is larger than 256 MiB.";

    /// <summary>Opens the archive and reads its manifest (step 2).</summary>
    /// <param name="path">The archive file.</param>
    /// <returns>The open archive (caller disposes) and its manifest.</returns>
    public static (ZipArchive Zip, BackupManifest Manifest) Open(string path)
    {
        var zip = OpenZip(path);
        try
        {
            return (zip, ReadManifest(zip));
        }
        catch
        {
            zip.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Reads only <c>format</c>, <c>formatVersion</c> and the descriptive members of the manifest, without the other rules:
    /// for the list entry of an archive that <see cref="Open"/> refused as <c>archive-incompatible</c>.
    /// </summary>
    /// <param name="path">The archive file.</param>
    /// <returns>The header.</returns>
    public static ManifestHeader ReadHeader(string path)
    {
        using var zip = OpenZip(path);
        return Deserialize<ManifestHeader>(ManifestBytes(zip));
    }

    /// <summary>
    /// Reads and checks the manifest: entry count, size (the decompressed read stops at 256 MiB + 1 byte whatever the
    /// header declares), JSON, then <c>format</c> and the <c>formatVersion</c> major before every other rule.
    /// </summary>
    /// <param name="zip">The archive.</param>
    /// <returns>The manifest.</returns>
    public static BackupManifest ReadManifest(ZipArchive zip)
    {
        ArgumentNullException.ThrowIfNull(zip);
        var bytes = ManifestBytes(zip);
        var header = Deserialize<ManifestHeader>(bytes);
        ManifestRules.CheckFormat(header.Format, header.FormatVersion);
        return ManifestRules.Check(Deserialize<BackupManifest>(bytes));
    }

    /// <summary>Checks every central-directory entry against the rules and the manifest (step 3).</summary>
    /// <param name="zip">The archive.</param>
    /// <param name="manifest">Its manifest.</param>
    public static void CheckEntries(ZipArchive zip, BackupManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(zip);
        ArgumentNullException.ThrowIfNull(manifest);
        var declared = manifest.Entries.ToDictionary(entry => entry.Path, StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in zip.Entries)
        {
            CheckEntry(entry, declared, seen);
        }

        if (manifest.Entries.FirstOrDefault(entry => !seen.Contains(entry.Path)) is { } missing)
        {
            throw Corrupt($"Entry \"{missing.Path}\" of the manifest is missing from the archive.");
        }
    }

    /// <summary>Copies an entry into a destination while hashing it; the copy stops one byte after the declared size.</summary>
    /// <param name="zip">The archive.</param>
    /// <param name="entry">The manifest entry.</param>
    /// <param name="destination">Where the bytes go (<see cref="Stream.Null"/> for verify).</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    public static async Task CopyVerifiedAsync(ZipArchive zip, ManifestEntry entry, Stream destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(zip);
        ArgumentNullException.ThrowIfNull(entry);
        await using var hashing = new HashingLimitedStream(destination, entry.SizeBytes);
        try
        {
            await using var source = zip.GetEntry(entry.Path)!.Open();
            await source.CopyToAsync(hashing, cancellationToken);
        }
        catch (InvalidDataException exception)
        {
            throw new LibraryJobException(LibraryJobErrorCodeDto.ArchiveCorrupt, $"Entry \"{entry.Path}\" is damaged or larger than declared.", exception);
        }

        if (hashing.Count != entry.SizeBytes || !string.Equals(hashing.Sha256(), entry.Sha256, StringComparison.Ordinal))
        {
            throw Corrupt($"Entry \"{entry.Path}\" does not match its recorded size and SHA-256.");
        }
    }

    /// <summary>An <c>archive-invalid</c> failure.</summary>
    /// <param name="message">Readable message.</param>
    /// <param name="inner">Cause, if any.</param>
    /// <returns>The exception.</returns>
    public static LibraryJobException Invalid(string message, Exception? inner = null) =>
        inner is null ? new LibraryJobException(LibraryJobErrorCodeDto.ArchiveInvalid, message) : new LibraryJobException(LibraryJobErrorCodeDto.ArchiveInvalid, message, inner);

    /// <summary>An <c>archive-unsafe</c> failure.</summary>
    /// <param name="message">Readable message ending with "nothing was changed".</param>
    /// <returns>The exception.</returns>
    public static LibraryJobException Unsafe(string message) => new(LibraryJobErrorCodeDto.ArchiveUnsafe, message);

    /// <summary>An <c>archive-corrupt</c> failure.</summary>
    /// <param name="message">Readable message.</param>
    /// <returns>The exception.</returns>
    public static LibraryJobException Corrupt(string message) => new(LibraryJobErrorCodeDto.ArchiveCorrupt, message);

    private static void CheckEntry(ZipArchiveEntry entry, Dictionary<string, ManifestEntry> declared, HashSet<string> seen)
    {
        var name = entry.FullName;
        var kind = LibraryLayout.ClassifyEntry(name);
        if (kind is ArchiveEntryKind.Unsafe or ArchiveEntryKind.Foreign || IsLink(entry) || !seen.Add(name))
        {
            throw Unsafe($"Entry \"{Printable(name)}\" is outside the library layout; nothing was changed.");
        }

        if (kind is ArchiveEntryKind.Directory or ArchiveEntryKind.Manifest or ArchiveEntryKind.Readme)
        {
            return;
        }

        if (!declared.TryGetValue(name, out var expected))
        {
            throw Unsafe($"Entry \"{Printable(name)}\" is not listed in the manifest; nothing was changed.");
        }

        if (expected.SizeBytes != entry.Length)
        {
            throw Corrupt($"Entry \"{Printable(name)}\" is larger or smaller than declared; nothing was changed.");
        }
    }

    private static bool IsLink(ZipArchiveEntry entry) => ((entry.ExternalAttributes >> 16) & UnixTypeMask) == UnixLinkMode;

    private static string Printable(string name)
    {
        var cleaned = new string([.. name.Take(200).Select(character => char.IsControl(character) ? '?' : character)]);
        return name.Length > 200 ? cleaned + "..." : cleaned;
    }

    private static ZipArchive OpenZip(string path)
    {
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            if (ZipDirectory.DeclaredEntries(stream) > LibraryCareLimits.MaxArchiveEntries)
            {
                throw Invalid(TooManyEntries);
            }

            return new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        }
        catch (InvalidDataException exception)
        {
            stream.Dispose();
            throw Invalid("The file is not a ZIP archive.", exception);
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    private static ReadOnlyMemory<byte> ManifestBytes(ZipArchive zip)
    {
        try
        {
            if (zip.Entries.Count > LibraryCareLimits.MaxArchiveEntries)
            {
                throw Invalid(TooManyEntries);
            }

            var entry = zip.GetEntry(LibraryLayout.ManifestEntry) ?? throw Invalid("Not a StemStudio backup (no manifest.json).");
            if (entry.Length > LibraryCareLimits.MaxManifestBytes)
            {
                throw Invalid(ManifestTooLarge);
            }

            using var buffer = new MemoryStream();
            using (var source = entry.Open())
            using (var bounded = new HashingLimitedStream(buffer, LibraryCareLimits.MaxManifestBytes))
            {
                source.CopyTo(bounded);
            }

            return buffer.GetBuffer().AsMemory(0, (int)buffer.Length);
        }
        catch (InvalidDataException exception)
        {
            throw Invalid("The archive's central directory or manifest is damaged or larger than 256 MiB.", exception);
        }
    }

    private static T Deserialize<T>(ReadOnlyMemory<byte> bytes)
        where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(bytes.Span, ManifestJson.Options) ?? throw Invalid("The manifest is empty.");
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            throw Invalid("The manifest is not valid.", exception);
        }
    }
}

/// <summary>
/// The members of a manifest that are read before the rules (format check first, then display of another format's
/// archive); every member is optional and loosely typed, so another format's shape still yields its version.
/// </summary>
public sealed record ManifestHeader
{
    /// <summary><c>format</c>.</summary>
    public string? Format { get; init; }

    /// <summary><c>formatVersion</c>.</summary>
    public string? FormatVersion { get; init; }

    /// <summary><c>backupId</c>, unchecked.</summary>
    public JsonElement? BackupId { get; init; }

    /// <summary><c>createdAt</c>, unchecked.</summary>
    public JsonElement? CreatedAt { get; init; }

    /// <summary><c>app</c>, unchecked.</summary>
    public JsonElement? App { get; init; }
}
