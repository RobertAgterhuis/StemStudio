using System.Globalization;
using StemStudio.Contracts.LibraryCare;
using StemStudio.Web.Services;

namespace StemStudio.Web.LibraryCare;

/// <summary>What the library job panel shows of a job (ui-test-ids 1.12.0 <c>library-job-*</c>).</summary>
/// <param name="Job">The job.</param>
public sealed record JobView(LibraryJobDto Job)
{
    /// <summary>The item word per collection name of a restore report (backend-013-us5 "Contract readings" 2).</summary>
    private static readonly Dictionary<string, string> ItemWords = new(StringComparer.Ordinal)
    {
        ["tracks"] = "Track",
        ["mixes"] = "Mix",
        ["styles"] = "Style",
        ["lyricsSheets"] = "Lyrics sheet",
        ["voiceProfiles"] = "Voice profile",
        ["assets"] = "Library item",
        ["generations"] = "Generation",
        ["agentConversations"] = "Agent conversation",
    };

    /// <summary>True while queued or running.</summary>
    public bool Active => Job.Status is LibraryJobStatusDto.Queued or LibraryJobStatusDto.Running;

    /// <summary>The panel title, e.g. "Backup running" or "Last check: failed".</summary>
    public string Title => Active
        ? $"{CareText.Kind(Job.Kind)} {CareText.Status(Job.Status)}"
        : $"Last {CareText.Kind(Job.Kind).ToLowerInvariant()}: {CareText.Status(Job.Status)}";

    /// <summary>Progress 0-100 (100 once completed).</summary>
    public int Progress => Job.Status == LibraryJobStatusDto.Completed ? 100 : Math.Clamp(Job.Progress, 0, 100);

    /// <summary>The stage text.</summary>
    public string Stage => CareText.Stage(Job);

    /// <summary><c>aria-valuetext</c> of the progress bar: "&lt;n&gt; % - &lt;stage text&gt;".</summary>
    public string ValueText => string.Create(CultureInfo.InvariantCulture, $"{Progress} % - {Stage}");

    /// <summary>"&lt;done&gt; of &lt;total&gt;", or null before the total is known.</summary>
    public string? Bytes => Job.BytesTotal > 0 ? $"{ByteSize.Format(Job.BytesDone)} of {ByteSize.Format(Job.BytesTotal)}" : null;

    /// <summary>The polite live-region text; it changes only with the status or the stage, so it is not read every poll.</summary>
    public string Announcement => $"{Title}. {Stage}.";

    /// <summary>The report summary of a completed job, or null.</summary>
    public string? ReportSummary => Job.Status != LibraryJobStatusDto.Completed ? null : Job.Report switch
    {
        BackupReportDto backup => $"Backup {backup.FileName} written: {CareText.Count(backup.Entries, "file")}, {ByteSize.Format(backup.Bytes)}.",
        VerifyReportDto { Ok: true } verify => $"The archive is complete and readable: {CareText.Count(verify.EntriesChecked, "file")} checked, {ByteSize.Format(verify.BytesChecked)}.",
        VerifyReportDto verify => $"The check found a problem: {verify.Problem?.Message ?? "the archive is damaged"}.",
        RestoreReportDto restore => RestoreSummary(restore),
        _ => $"{CareText.Kind(Job.Kind)} completed.",
    };

    /// <summary>One line per category of the report ("Stems: 24 files, 812 MB").</summary>
    public IReadOnlyList<string> ReportLines => Job.Report switch
    {
        BackupReportDto backup => Lines(backup.Included),
        RestoreReportDto restore => Lines(restore.Restored),
        _ => [],
    };

    /// <summary>Warnings and skipped items of the report.</summary>
    public IReadOnlyList<string> Warnings => Job.Report switch
    {
        BackupReportDto backup => [.. backup.Warnings.Select(w => w.Message)],
        RestoreReportDto restore => [.. restore.Warnings, .. restore.Skipped.Select(SkippedText)],
        _ => [],
    };

    /// <summary>The text of a skipped (or renamed) item of a restore.</summary>
    /// <param name="item">The item.</param>
    /// <returns>For example "Track \"Song\": already in your library".</returns>
    public static string SkippedText(SkippedItemDto item)
    {
        var name = item.Name is { } n ? $"\"{n}\"" : item.Id;
        var what = $"{ItemWord(item.Category)} {name}";
        return item.RenamedTo is { } renamed
            ? $"{what} added as \"{renamed}\" (the name was taken)"
            : $"{what}: {Reason(item.Reason)}";
    }

    private static string Reason(SkippedItemReasonDto reason) => reason switch
    {
        SkippedItemReasonDto.SameId => "already in your library",
        SkippedItemReasonDto.SameSource => "the same song is already in your library",
        SkippedItemReasonDto.DependencySkipped => "the item it belongs to was skipped",
        SkippedItemReasonDto.ConsentInvalid => "its consent record is not valid",
        SkippedItemReasonDto.Option => "left out by your choice",
        _ => "its file is missing from the archive",
    };

    private static string RestoreSummary(RestoreReportDto restore)
    {
        var what = restore.Mode switch
        {
            RestoreReportModeDto.Undo => "The previous library is back.",
            RestoreReportModeDto.Merge => "The missing items of the backup were added to your library.",
            _ => "The backup was restored.",
        };
        var migrated = restore.MigratedFrom is { } from ? $" It was updated from an older StemStudio ({from})." : string.Empty;
        var kept = restore.PreviousLibraryKept ? " Your previous library is kept below until you discard it." : string.Empty;
        return what + migrated + kept;
    }

    private static List<string> Lines(IEnumerable<CategoryCountDto> counts) =>
        [.. counts.Select(c => $"{CareText.Category(c.Category)}: {CareText.Count(c.Files, "file")}, {ByteSize.Format(c.Bytes)}")];

    /// <summary>The item word of a collection name; an unknown name becomes words ("practiceLoops" -> "Practice loop").</summary>
    private static string ItemWord(string category) =>
        ItemWords.TryGetValue(category, out var word) ? word
        : category.Length == 0 ? "Item"
        : Words(Singular(category));

    private static string Singular(string name) => name.Length > 1 && name.EndsWith('s') && !name.EndsWith("ss", StringComparison.Ordinal) ? name[..^1] : name;

    private static string Words(string camel)
    {
        var words = string.Concat(camel.Select(c => char.IsUpper(c) ? $" {char.ToLowerInvariant(c)}" : c.ToString())).TrimStart();
        return char.ToUpperInvariant(words[0]) + words[1..];
    }
}
