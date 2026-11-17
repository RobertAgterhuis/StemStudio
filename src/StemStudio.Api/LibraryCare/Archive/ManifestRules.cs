using System.Text.RegularExpressions;
using StemStudio.Contracts.LibraryCare;
using StemStudio.Domain.LibraryCare;

namespace StemStudio.Api.LibraryCare.Archive;

/// <summary>
/// The checks of <c>backup-manifest.schema.json</c> that a reader needs (no JSON Schema library: required members come from
/// the C# <c>required</c> modifiers, the rest is checked here): constants, version major 1, patterns, limits and totals.
/// </summary>
public static partial class ManifestRules
{
    /// <summary>Reason of an archive in another format version.</summary>
    public const string IncompatibleText = "The backup was made by a newer StemStudio; update StemStudio first.";

    private const string UnknownFormat = "Not a StemStudio backup (unknown format).";

    /// <summary>Checks a deserialised manifest.</summary>
    /// <param name="manifest">The manifest.</param>
    /// <returns>The same manifest.</returns>
    /// <exception cref="Jobs.LibraryJobException"><c>archive-invalid</c> or <c>archive-incompatible</c>.</exception>
    public static BackupManifest Check(BackupManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        CheckFormat(manifest.Format, manifest.FormatVersion);
        if (manifest.HashAlgorithm != "sha-256" || manifest.Encryption != "none")
        {
            throw ArchiveReader.Invalid(UnknownFormat);
        }

        CheckLists(manifest);
        return manifest;
    }

    /// <summary>
    /// The format check that runs before every other rule (CTO decision 013 "formatVersion 2.0.0"): another <c>format</c>
    /// or no <c>formatVersion</c> is <c>archive-invalid</c>, a version other than 1.x is <c>archive-incompatible</c>.
    /// </summary>
    /// <param name="format">The <c>format</c> member.</param>
    /// <param name="formatVersion">The <c>formatVersion</c> member.</param>
    /// <exception cref="Jobs.LibraryJobException"><c>archive-invalid</c> or <c>archive-incompatible</c>.</exception>
    public static void CheckFormat(string? format, string? formatVersion)
    {
        if (format != LibraryCareLimits.ArchiveFormat || formatVersion is null)
        {
            throw ArchiveReader.Invalid(UnknownFormat);
        }

        if (!VersionPattern().IsMatch(formatVersion))
        {
            throw new Jobs.LibraryJobException(LibraryJobErrorCodeDto.ArchiveIncompatible, IncompatibleText);
        }
    }

    private static void CheckLists(BackupManifest manifest)
    {
        if (HasNullElements(manifest) || !EntriesValid(manifest) || !HeaderValid(manifest))
        {
            throw ArchiveReader.Invalid("The manifest is not valid.");
        }
    }

    private static bool HasNullElements(BackupManifest manifest) =>
        manifest.Entries.Contains(null!) || manifest.VoiceProfiles.Contains(null!) || manifest.Warnings.Contains(null!)
        || manifest.Schema.Migrations.Contains(null!) || manifest.Contents.Excluded.Contains(null!);

    private static bool EntriesValid(BackupManifest manifest) =>
        manifest.Entries.Count is > 0 and <= LibraryCareLimits.MaxArchiveEntries
        && manifest.Entries.All(entry => Sha256Pattern().IsMatch(entry.Sha256) && entry.SizeBytes >= 0 && entry.Path.Length <= LibraryLayout.MaxEntryNameLength)
        && manifest.Entries.Select(entry => entry.Path).Distinct(StringComparer.Ordinal).Count() == manifest.Entries.Count
        && manifest.Totals.Entries == manifest.Entries.Count && manifest.Totals.Bytes == manifest.Entries.Sum(entry => entry.SizeBytes);

    private static bool HeaderValid(BackupManifest manifest) =>
        manifest.Schema.Migrations.Count > 0 && manifest.Schema.Migrations.All(MigrationPattern().IsMatch)
        && manifest.VoiceProfiles.All(profile => Sha256Pattern().IsMatch(profile.SampleSha256) && Guid.TryParse(profile.Id, out _))
        && Guid.TryParse(manifest.BackupId, out _);

    [GeneratedRegex(@"\A1\.(0|[1-9][0-9]{0,3})\.(0|[1-9][0-9]{0,3})\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex VersionPattern();

    [GeneratedRegex(@"\A[0-9]{14}_[A-Za-z0-9]{1,120}\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex MigrationPattern();

    [GeneratedRegex(@"\A[0-9a-f]{64}\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Sha256Pattern();
}
