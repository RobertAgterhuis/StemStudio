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
    /// <summary>Checks a deserialised manifest.</summary>
    /// <param name="manifest">The manifest.</param>
    /// <returns>The same manifest.</returns>
    /// <exception cref="Jobs.LibraryJobException"><c>archive-invalid</c> or <c>archive-incompatible</c>.</exception>
    public static BackupManifest Check(BackupManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if (manifest.Format != LibraryCareLimits.ArchiveFormat || manifest.HashAlgorithm != "sha-256" || manifest.Encryption != "none")
        {
            throw ArchiveReader.Invalid("Not a StemStudio backup (unknown format).");
        }

        if (!VersionPattern().IsMatch(manifest.FormatVersion))
        {
            throw new Jobs.LibraryJobException(LibraryJobErrorCodeDto.ArchiveIncompatible, "The backup was made by a newer StemStudio; update StemStudio first.");
        }

        CheckLists(manifest);
        return manifest;
    }

    private static void CheckLists(BackupManifest manifest)
    {
        var valid = manifest.Entries.Count is > 0 and <= LibraryCareLimits.MaxArchiveEntries
            && manifest.Schema.Migrations.Count > 0 && manifest.Schema.Migrations.All(MigrationPattern().IsMatch)
            && manifest.Entries.All(entry => Sha256Pattern().IsMatch(entry.Sha256) && entry.SizeBytes >= 0 && entry.Path.Length <= LibraryLayout.MaxEntryNameLength)
            && manifest.VoiceProfiles.All(profile => Sha256Pattern().IsMatch(profile.SampleSha256) && Guid.TryParse(profile.Id, out _))
            && manifest.Totals.Entries == manifest.Entries.Count && manifest.Totals.Bytes == manifest.Entries.Sum(entry => entry.SizeBytes)
            && Guid.TryParse(manifest.BackupId, out _);
        if (!valid)
        {
            throw ArchiveReader.Invalid("The manifest is not valid.");
        }
    }

    [GeneratedRegex(@"\A1\.(0|[1-9][0-9]{0,3})\.(0|[1-9][0-9]{0,3})\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex VersionPattern();

    [GeneratedRegex(@"\A[0-9]{14}_[A-Za-z0-9]{1,120}\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex MigrationPattern();

    [GeneratedRegex(@"\A[0-9a-f]{64}\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Sha256Pattern();
}
