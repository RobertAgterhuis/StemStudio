using Radzen;
using StemStudio.Contracts;
using StemStudio.Contracts.Tracks;

namespace StemStudio.Web.Services;

/// <summary>Display rules and texts of the detail phase (FR-007, FR-017, data-model.md 2.2, ui-test-ids 1.1.0).</summary>
public static class DetailPhaseText
{
    /// <summary>Songs longer than this (10 minutes) get the long-duration notice before the phase runs.</summary>
    public const double LongSongSeconds = 600;

    /// <summary>The notice shown while the phase is queued or running (FR-017, US3 AS1).</summary>
    public const string DurationNotice =
        "Detail separation takes considerably longer than the standard separation: about 20 to 30 minutes for a "
        + "4-minute song on a fast computer, longer on slower ones. The standard stems stay playable meanwhile.";

    /// <summary>The extra sentence for songs longer than <see cref="LongSongSeconds"/>.</summary>
    public const string LongSongNotice = "This song is longer than 10 minutes, so the detail phase may take a long time.";

    /// <summary>The limitations of detail separation (FR-017, constitution V).</summary>
    public const string LimitationHelp =
        "Detail separation is experimental: quality varies per song and instrument, and some instruments cannot be "
        + "separated (bass is never split, organ is part of keys, and instruments that blend together may stay in "
        + "\"rest\"). Only detail stems that are audible in this song are offered. The detail phase takes "
        + "considerably longer than the standard separation.";

    private static readonly Dictionary<DetailPhaseStatusDto, string> StatusTexts = new()
    {
        [DetailPhaseStatusDto.None] = "none",
        [DetailPhaseStatusDto.Queued] = "queued",
        [DetailPhaseStatusDto.Running] = "running",
        [DetailPhaseStatusDto.Completed] = "completed",
        [DetailPhaseStatusDto.Failed] = "failed",
        [DetailPhaseStatusDto.Cancelled] = "cancelled",
    };

    private static readonly Dictionary<ParentOutcomeKindDto, (string Kind, string Text)> OutcomeTexts = new()
    {
        [ParentOutcomeKindDto.Split] = ("split", "Split into"),
        [ParentOutcomeKindDto.SkippedSilent] = ("skipped-silent", "Skipped: (almost) silent"),
        [ParentOutcomeKindDto.NothingFurther] = ("nothing-further", "Nothing further to separate"),
        [ParentOutcomeKindDto.NotReliable] = ("not-reliable", "Split was not reliable"),
        [ParentOutcomeKindDto.NotSupported] = ("not-supported", "Not split"),
    };

    /// <summary>The contract text of a status (<c>none</c>, <c>queued</c>, ...), also shown to the user.</summary>
    /// <param name="status">The status.</param>
    /// <returns>The lower-case status text.</returns>
    public static string Status(DetailPhaseStatusDto status) => StatusTexts[status];

    /// <summary>True while the phase is queued or running (polling continues, cancel is possible, delete is refused).</summary>
    /// <param name="status">The status.</param>
    /// <returns>Whether the phase is active.</returns>
    public static bool IsActive(DetailPhaseStatusDto status) => status is DetailPhaseStatusDto.Queued or DetailPhaseStatusDto.Running;

    /// <summary>Badge colour of a status.</summary>
    /// <param name="status">The status.</param>
    /// <returns>Success, danger, info (active) or light.</returns>
    public static BadgeStyle Badge(DetailPhaseStatusDto status) => status switch
    {
        DetailPhaseStatusDto.Completed => BadgeStyle.Success,
        DetailPhaseStatusDto.Failed => BadgeStyle.Danger,
        _ => IsActive(status) ? BadgeStyle.Info : BadgeStyle.Light,
    };

    /// <summary>The kebab-case outcome kind of the contract (<c>data-outcome</c>).</summary>
    /// <param name="outcome">The outcome kind.</param>
    /// <returns>For example <c>skipped-silent</c>.</returns>
    public static string Kind(ParentOutcomeKindDto outcome) => OutcomeTexts[outcome].Kind;

    /// <summary>The user text of an outcome, e.g. "Split into kick, snare" (kept sub-stems without rest).</summary>
    /// <param name="outcome">The parent outcome.</param>
    /// <returns>The text.</returns>
    public static string Outcome(ParentOutcomeDto outcome)
    {
        var text = OutcomeTexts[outcome.Outcome].Text;
        if (outcome.Outcome != ParentOutcomeKindDto.Split)
        {
            return text;
        }

        var kept = OrderedStems(outcome).Select(s => s.Name).Where(n => n != DetailNames.Rest);
        return $"{text} {string.Join(", ", kept)}";
    }

    /// <summary>The detail stems of an outcome in display order (<c>rest</c> last); names not allowed for the parent are dropped.</summary>
    /// <param name="outcome">The parent outcome.</param>
    /// <returns>The ordered stems.</returns>
    public static IReadOnlyList<DetailStemDto> OrderedStems(ParentOutcomeDto outcome)
    {
        var byName = outcome.Stems.DistinctBy(s => s.Name).ToDictionary(s => s.Name, StringComparer.Ordinal);
        return DetailNames.AllowedFor(outcome.Parent).Where(byName.ContainsKey).Select(n => byName[n]).ToList();
    }

    /// <summary>The long-duration notice for a completed track, or null when none is shown.</summary>
    /// <param name="track">The track.</param>
    /// <returns>The notice: while the phase is active, and for songs over 10 minutes until the phase completes.</returns>
    public static string? Notice(TrackDto track)
    {
        var status = track.Detail.Status;
        var longSong = track.DurationSeconds > LongSongSeconds && status != DetailPhaseStatusDto.Completed;
        return (IsActive(status), longSong) switch
        {
            (true, true) => $"{DurationNotice} {LongSongNotice}",
            (true, false) => DurationNotice,
            (false, true) => LongSongNotice,
            _ => null,
        };
    }
}
