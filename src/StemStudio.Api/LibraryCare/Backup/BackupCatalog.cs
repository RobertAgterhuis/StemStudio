using System.Collections.Concurrent;
using StemStudio.Api.LibraryCare.Archive;
using StemStudio.Api.LibraryCare.Jobs;
using StemStudio.Contracts.LibraryCare;
using StemStudio.Domain.LibraryCare;
using StemStudio.Persistence;

namespace StemStudio.Api.LibraryCare.Backup;

/// <summary>The migrations this StemStudio knows (restore compatibility, research R-08).</summary>
/// <param name="scopes">Scope factory for a context.</param>
public sealed class MigrationCatalog(IServiceScopeFactory scopes)
{
    private IReadOnlyList<string>? _all;

    /// <summary>Every migration id of the app, in order.</summary>
    public IReadOnlyList<string> All => _all ??= Load();

    /// <summary>The compatibility of an archive's migrations.</summary>
    /// <param name="migrations">The archive's migration ids.</param>
    /// <returns>The compatibility.</returns>
    public CompatibilityDto Compatibility(IReadOnlyList<string> migrations)
    {
        ArgumentNullException.ThrowIfNull(migrations);
        var unknown = migrations.Except(All, StringComparer.Ordinal).ToList();
        return unknown.Count > 0
            ? new CompatibilityDto { Restorable = false, Reason = "The backup was made by a newer StemStudio; update StemStudio first.", MigrationNeeded = false }
            : new CompatibilityDto { Restorable = true, Reason = null, MigrationNeeded = !string.Equals(migrations[^1], All[^1], StringComparison.Ordinal) };
    }

    private List<string> Load()
    {
        using var scope = scopes.CreateScope();
        return [.. Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.GetMigrations(scope.ServiceProvider.GetRequiredService<StemStudioDbContext>().Database)];
    }
}

/// <summary>
/// Archives in the backups folder (<c>listBackups</c>, <c>getBackup</c>): every <c>*.zip</c> with a valid name, the manifest
/// read from the central directory, cached per file name, size and modification time.
/// </summary>
/// <param name="paths">Library-care folders.</param>
/// <param name="migrations">Known migrations.</param>
public sealed class BackupCatalog(LibraryCarePaths paths, MigrationCatalog migrations)
{
    /// <summary>Reason of a file that is not a StemStudio backup.</summary>
    public const string NotABackup = "Not a StemStudio backup.";

    private readonly ConcurrentDictionary<(string Name, long Size, DateTime Modified), BackupDetailsDto> _cache = new();

    /// <summary>Every archive, newest first.</summary>
    /// <returns>The summaries.</returns>
    public IReadOnlyList<BackupSummaryDto> List() => Directory.Exists(paths.BackupsDirectory)
        ? [.. new DirectoryInfo(paths.BackupsDirectory).EnumerateFiles("*.zip").Where(IsArchive).Select(Read).OrderByDescending(archive => archive.ModifiedAt).Select(Summary)]
        : [];

    /// <summary>One archive with its details.</summary>
    /// <param name="fileName">Archive name.</param>
    /// <returns>The details, or null when the file does not exist.</returns>
    public BackupDetailsDto? Find(string fileName) => File(fileName) is { } file ? Read(file) : null;

    /// <summary>The archive file when the name is valid and it is a regular file in the backups folder.</summary>
    /// <param name="fileName">Archive name.</param>
    /// <returns>The file, or null.</returns>
    public FileInfo? File(string fileName)
    {
        if (!ArchiveNames.IsValid(fileName))
        {
            return null;
        }

        var file = new FileInfo(paths.Archive(fileName));
        return file.Exists && IsArchive(file) ? file : null;
    }

    private static bool IsArchive(FileInfo file) => ArchiveNames.IsValid(file.Name) && (file.Attributes & FileAttributes.ReparsePoint) == 0;

    private static BackupSummaryDto Summary(BackupDetailsDto details) => new()
    {
        FileName = details.FileName,
        SizeBytes = details.SizeBytes,
        ModifiedAt = details.ModifiedAt,
        Recognised = details.Recognised,
        Reason = details.Reason,
        BackupId = details.BackupId,
        CreatedAt = details.CreatedAt,
        AppVersion = details.AppVersion,
        BackendApi = details.BackendApi,
        FormatVersion = details.FormatVersion,
        Included = details.Included,
        Counts = details.Counts,
        Compatibility = details.Compatibility,
    };

    private BackupDetailsDto Read(FileInfo file) =>
        _cache.GetOrAdd((file.Name, file.Length, file.LastWriteTimeUtc), _ => Describe(file));

    private BackupDetailsDto Describe(FileInfo file)
    {
        try
        {
            var (zip, manifest) = ArchiveReader.Open(file.FullName);
            zip.Dispose();
            return BackupDescription.Recognised(file, manifest, migrations.Compatibility(manifest.Schema.Migrations));
        }
        catch (LibraryJobException exception)
        {
            return BackupDescription.Unrecognised(file, exception.Code == LibraryJobErrorCodeDto.ArchiveIncompatible ? exception.Message : NotABackup, exception.Message);
        }
    }
}

/// <summary>Maps manifests to the contract's archive descriptions.</summary>
public static class BackupDescription
{
    /// <summary>A recognised archive.</summary>
    /// <param name="file">The file.</param>
    /// <param name="manifest">Its manifest.</param>
    /// <param name="compatibility">Its compatibility.</param>
    /// <returns>The details.</returns>
    public static BackupDetailsDto Recognised(FileInfo file, BackupManifest manifest, CompatibilityDto compatibility)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(manifest);
        return new BackupDetailsDto
        {
            FileName = file.Name,
            SizeBytes = file.Length,
            ModifiedAt = new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero),
            Recognised = true,
            Reason = null,
            BackupId = Guid.Parse(manifest.BackupId),
            CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(manifest.CreatedAt, DateTimeKind.Utc)),
            AppVersion = manifest.App.Version,
            BackendApi = manifest.App.BackendApi,
            FormatVersion = manifest.FormatVersion,
            Included = [.. manifest.Contents.Included.Select(BackupOptionsMapping.ToDto)],
            Counts = Counts(manifest.Counts),
            Compatibility = compatibility,
            Excluded = [.. manifest.Contents.Excluded.Select(exclusion => BackupOptionsMapping.ToDto(exclusion.Category))],
            Entries = manifest.Totals.Entries,
            TotalBytes = manifest.Totals.Bytes,
            SchemaLastMigration = manifest.Schema.LastMigration,
            VoiceProfiles = [.. manifest.VoiceProfiles.Select(Proof)],
            Warnings = manifest.Warnings,
        };
    }

    /// <summary>A file that is not a readable StemStudio backup.</summary>
    /// <param name="file">The file.</param>
    /// <param name="compatibilityReason">Why it cannot be restored.</param>
    /// <param name="reason">Why it is not recognised.</param>
    /// <returns>The details.</returns>
    public static BackupDetailsDto Unrecognised(FileInfo file, string compatibilityReason, string reason)
    {
        ArgumentNullException.ThrowIfNull(file);
        return new BackupDetailsDto
        {
            FileName = file.Name,
            SizeBytes = file.Length,
            ModifiedAt = new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero),
            Recognised = false,
            Reason = reason,
            BackupId = null,
            CreatedAt = null,
            AppVersion = null,
            BackendApi = null,
            FormatVersion = null,
            Included = [],
            Counts = null,
            Compatibility = new CompatibilityDto { Restorable = false, Reason = compatibilityReason, MigrationNeeded = false },
            Excluded = [],
            Entries = 0,
            TotalBytes = 0,
            SchemaLastMigration = string.Empty,
            VoiceProfiles = [],
            Warnings = [],
        };
    }

    private static ArchiveCountsDto Counts(ManifestCounts counts) => new()
    {
        Tracks = counts.Tracks,
        Mixes = counts.Mixes,
        Assets = counts.Assets,
        Generations = counts.Generations,
        Styles = counts.Styles,
        LyricsSheets = counts.LyricsSheets,
        VoiceProfiles = counts.VoiceProfiles,
    };

    private static VoiceProfileProofDto Proof(ManifestVoiceProfile profile)
    {
        var valid = ConsentProofRules.IsStatementValid(profile.Consent.StatementVersion, profile.Consent.StatementSha256);
        return new VoiceProfileProofDto
        {
            Id = Guid.Parse(profile.Id),
            DisplayName = profile.DisplayName,
            StatementVersion = profile.Consent.StatementVersion,
            PersonName = profile.Consent.PersonName,
            ConsentedAt = new DateTimeOffset(DateTime.SpecifyKind(profile.Consent.ConsentedAt, DateTimeKind.Utc)),
            Valid = valid,
            Reason = valid ? null : "The consent statement is unknown or does not match.",
        };
    }
}
