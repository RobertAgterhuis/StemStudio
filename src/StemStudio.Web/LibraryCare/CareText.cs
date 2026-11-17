using System.Globalization;
using System.Text;
using StemStudio.Contracts.LibraryCare;
using StemStudio.Web.Services;

namespace StemStudio.Web.LibraryCare;

/// <summary>Readable texts and test-id parts of the Library care screens (013, ui-test-ids 1.12.0 "Template parts").</summary>
public static class CareText
{
    private static readonly Dictionary<string, string> Categories = new(StringComparer.Ordinal)
    {
        ["database"] = "Database (tracks, mixes, settings)",
        ["stems"] = "Stems",
        ["artwork"] = "Artwork",
        ["pendingUploads"] = "Pending uploads",
        ["detailStems"] = "Detail stems",
        ["exports"] = "Exports",
        ["scores"] = "Sheet music",
        ["generations"] = "Generations",
        ["recordings"] = "Recordings",
        ["voiceProfiles"] = "Voice profiles",
        ["leftovers"] = "Leftovers",
        ["restoreStaging"] = "Restore in progress",
        ["previousLibrary"] = "Previous library",
        ["workerSeparationCache"] = "Worker separation cache",
        ["workerDetailCache"] = "Worker detail cache",
        ["workerJobFiles"] = "Worker job files",
        ["workerScratch"] = "Worker scratch files",
    };

    private static readonly Dictionary<LibraryJobStageDto, string> Stages = new()
    {
        [LibraryJobStageDto.Snapshot] = "Copying the database",
        [LibraryJobStageDto.Scrub] = "Preparing the database copy",
        [LibraryJobStageDto.Files] = "Writing files",
        [LibraryJobStageDto.Manifest] = "Writing the contents list",
        [LibraryJobStageDto.Check] = "Checking the archive",
        [LibraryJobStageDto.Validate] = "Checking the archive",
        [LibraryJobStageDto.Extract] = "Unpacking files",
        [LibraryJobStageDto.Database] = "Checking the database",
        [LibraryJobStageDto.Switch] = "Switching to the restored library",
        [LibraryJobStageDto.Reconcile] = "Finishing",
    };

    /// <summary>The kebab-case test-id part of a contract enum value (<c>detailStems</c> -&gt; <c>detail-stems</c>).</summary>
    /// <typeparam name="T">A contract enum with a string converter.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns>The id part.</returns>
    public static string Kebab<T>(T value)
        where T : struct, Enum
    {
        var wire = WireName.Of(value);
        var text = new StringBuilder(wire.Length + 4);
        foreach (var c in wire)
        {
            text.Append(char.IsUpper(c) ? $"-{char.ToLowerInvariant(c)}" : c.ToString());
        }

        return text.ToString();
    }

    /// <summary>The readable name of a backup or storage category.</summary>
    /// <typeparam name="T"><see cref="BackupCategoryDto"/> or <see cref="StorageCategoryDto"/>.</typeparam>
    /// <param name="category">The category.</param>
    /// <returns>For example "Detail stems".</returns>
    public static string Category<T>(T category)
        where T : struct, Enum => Categories.TryGetValue(WireName.Of(category), out var name) ? name : WireName.Of(category);

    /// <summary>"Regenerable" or "Original".</summary>
    /// <param name="value">The class.</param>
    /// <returns>The badge text.</returns>
    public static string Class(RegenerabilityDto value) => value == RegenerabilityDto.Regenerable ? "Regenerable" : "Original";

    /// <summary>The job kind as a noun: "Backup", "Check", "Restore", "Undo restore".</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The text.</returns>
    public static string Kind(LibraryJobKindDto kind) => kind switch
    {
        LibraryJobKindDto.Backup => "Backup",
        LibraryJobKindDto.Verify => "Check",
        LibraryJobKindDto.Restore => "Restore",
        _ => "Undo restore",
    };

    /// <summary>The status as a phrase: "queued", "running", "completed", "failed", "cancelled".</summary>
    /// <param name="status">The status.</param>
    /// <returns>The text.</returns>
    public static string Status(LibraryJobStatusDto status) => WireName.Of(status);

    /// <summary>The stage text: the server's when it sent one, else the stage's own text.</summary>
    /// <param name="job">The job.</param>
    /// <returns>The stage text, or "Waiting to start" / the status for a job without stage.</returns>
    public static string Stage(LibraryJobDto job) => job.StageText
        ?? (job.Stage is { } stage ? Stages[stage] : job.Status == LibraryJobStatusDto.Queued ? "Waiting to start" : Capital(Status(job.Status)));

    /// <summary>Time left, for example "about 3 minutes left".</summary>
    /// <param name="seconds">Seconds, or null when unknown.</param>
    /// <returns>The text, or null.</returns>
    public static string? Eta(int? seconds) => seconds is { } value ? $"{TimeText.About(value)} left" : null;

    /// <summary>A date and time, for example "17 Nov 2026, 10:05".</summary>
    /// <param name="value">The time.</param>
    /// <returns>The text.</returns>
    public static string When(DateTimeOffset value) => value.ToString("d MMM yyyy, HH:mm", CultureInfo.InvariantCulture);

    /// <summary>A count with its noun, for example "1 track" or "3 tracks".</summary>
    /// <param name="count">The count.</param>
    /// <param name="noun">The singular noun.</param>
    /// <returns>The text.</returns>
    public static string Count(int count, string noun) =>
        string.Create(CultureInfo.InvariantCulture, $"{count} {noun}{(count == 1 ? string.Empty : "s")}");

    /// <summary>The item counts of an archive or library: "3 tracks, 1 mix, ...", zero counts left out.</summary>
    /// <param name="counts">The counts.</param>
    /// <returns>The text, or "Nothing" when every count is zero.</returns>
    public static string Counts(ArchiveCountsDto counts)
    {
        (int Count, string Noun)[] parts =
        [
            (counts.Tracks, "track"), (counts.Mixes, "mix"), (counts.Assets, "library item"), (counts.Generations, "generation"),
            (counts.Styles, "style"), (counts.LyricsSheets, "lyrics sheet"), (counts.VoiceProfiles, "voice profile"),
        ];
        var text = string.Join(", ", parts.Where(p => p.Count > 0).Select(p => p.Noun == "mix" ? MixCount(p.Count) : Count(p.Count, p.Noun)));
        return text.Length == 0 ? "Nothing" : text;
    }

    /// <summary>"Ready to restore", "Older version: updated when restored", "Not compatible: ..." or "Not a StemStudio backup: ...".</summary>
    /// <param name="archive">The archive.</param>
    /// <returns>The status text.</returns>
    public static string ArchiveStatus(BackupSummaryDto archive) => ArchiveRules.State(archive) switch
    {
        ArchiveRules.Unrecognised => $"Not a StemStudio backup: {archive.Reason ?? "the file has no readable contents list"}",
        ArchiveRules.Incompatible => $"Not compatible: {archive.Compatibility.Reason ?? "made by a newer StemStudio"}",
        ArchiveRules.Migration => "Made by an older StemStudio: updated when it is restored",
        _ => "Ready to restore",
    };

    private static string MixCount(int count) => count == 1 ? "1 mix" : string.Create(CultureInfo.InvariantCulture, $"{count} mixes");

    private static string Capital(string text) => char.ToUpperInvariant(text[0]) + text[1..];
}
