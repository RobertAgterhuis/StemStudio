using System.Globalization;
using StemStudio.Contracts.LibraryCare;
using StemStudio.Domain.LibraryCare;

namespace StemStudio.Api.LibraryCare.Archive;

/// <summary>Everything a manifest is built from.</summary>
public sealed record ManifestInput
{
    /// <summary>Backup id.</summary>
    public required Guid BackupId { get; init; }

    /// <summary>Snapshot time (UTC).</summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>Informational version of the API.</summary>
    public required string AppVersion { get; init; }

    /// <summary>Migration ids of the archived database, in order.</summary>
    public required IReadOnlyList<string> Migrations { get; init; }

    /// <summary>Optional categories the user selected.</summary>
    public required IReadOnlyCollection<BackupCategory> Selected { get; init; }

    /// <summary>Row counts.</summary>
    public required ManifestCounts Counts { get; init; }

    /// <summary>Written entries.</summary>
    public required IReadOnlyList<ManifestEntry> Entries { get; init; }

    /// <summary>Consent proofs.</summary>
    public required IReadOnlyList<ManifestVoiceProfile> VoiceProfiles { get; init; }

    /// <summary>Warnings.</summary>
    public required IReadOnlyList<ArchiveWarningDto> Warnings { get; init; }
}

/// <summary>Builds the manifest of backup-archive 1.0.0 (writer rule 3).</summary>
public static class ManifestBuilder
{
    /// <summary>Backend API contract version written into archives.</summary>
    public const string BackendApiVersion = "1.14.0";

    /// <summary>Builds the manifest: a category with entries is included; others are excluded by option or as empty.</summary>
    /// <param name="input">The input.</param>
    /// <returns>The manifest.</returns>
    public static BackupManifest Build(ManifestInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return new BackupManifest
        {
            BackupId = input.BackupId.ToString("D", CultureInfo.InvariantCulture),
            CreatedAt = DateTime.SpecifyKind(input.CreatedAt, DateTimeKind.Utc),
            App = new ManifestApp("StemStudio", input.AppVersion, BackendApiVersion),
            Schema = new ManifestSchema("sqlite", input.Migrations[^1], input.Migrations),
            Contents = Contents(input),
            Counts = input.Counts,
            Totals = new ManifestTotals(input.Entries.Count, input.Entries.Sum(entry => entry.SizeBytes)),
            Entries = input.Entries,
            VoiceProfiles = input.VoiceProfiles,
            Warnings = input.Warnings,
        };
    }

    private static ManifestContents Contents(ManifestInput input)
    {
        var present = input.Entries.Select(entry => entry.Category).ToHashSet();
        var included = BackupCategories.All.Where(present.Contains).ToList();
        var excluded = BackupCategories.All.Where(category => !present.Contains(category))
            .Select(category => new ManifestExclusion(category, IsLeftOut(category, input.Selected) ? ExclusionReason.Option : ExclusionReason.Empty))
            .ToList();
        return new ManifestContents(included, excluded);
    }

    private static bool IsLeftOut(BackupCategory category, IReadOnlyCollection<BackupCategory> selected) =>
        BackupCategories.IsOptional(category) && !selected.Contains(category);
}

/// <summary>Items, files and bytes per category of archive entries (backup and restore reports).</summary>
public static class CategoryCounts
{
    /// <summary>Counts the entries, leaving out entries of skipped entities.</summary>
    /// <param name="manifest">The manifest.</param>
    /// <param name="skippedEntities">Entity ids to leave out.</param>
    /// <returns>The counts in category order.</returns>
    public static IReadOnlyList<CategoryCountDto> Of(BackupManifest manifest, IReadOnlySet<string> skippedEntities)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        return
        [
            .. manifest.Entries.Where(entry => entry.Entity is null || !skippedEntities.Contains(entry.Entity.Id)).GroupBy(entry => entry.Category).OrderBy(group => group.Key)
                .Select(group => new CategoryCountDto
                {
                    Category = (BackupCategoryDto)(int)group.Key,
                    Items = group.Key == BackupCategory.Database ? 1 : group.Select(entry => entry.Entity?.Id).Distinct(StringComparer.Ordinal).Count(),
                    Files = group.Count(),
                    Bytes = group.Sum(entry => entry.SizeBytes),
                }),
        ];
    }
}
