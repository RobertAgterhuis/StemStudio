using System.Globalization;
using StemStudio.Contracts.Lyrics;

namespace StemStudio.Web.Lyrics;

/// <summary>Readable texts and attribute values of lyrics (feature 006, ui-test-ids 1.6.0).</summary>
public static class LyricsText
{
    private const string TrackPageHint = " You can enter them yourself on the track page.";

    /// <summary>The wire value of a status, used as <c>data-status</c>: <c>notFound</c>, <c>alignPending</c>.</summary>
    /// <param name="status">The status.</param>
    /// <returns>The camelCase value.</returns>
    public static string Value(LyricsStatusDto status)
    {
        var name = status.ToString();
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    /// <summary>The <c>data-sync</c> value of a timed document: <c>aligned</c>, <c>word</c> or <c>line</c>.</summary>
    /// <param name="lyrics">The lyrics.</param>
    /// <returns>The value, or null for a plain document.</returns>
    public static string? Sync(LyricsDto lyrics) => lyrics switch
    {
        { Origin: LyricsOriginDto.Aligned } => "aligned",
        { SyncLevel: SyncLevelDto.Word } => "word",
        { SyncLevel: SyncLevelDto.Line } => "line",
        _ => null,
    };

    /// <summary>Where the word times come from (Expert level, FR-015).</summary>
    /// <param name="lyrics">The lyrics.</param>
    /// <returns>For example "Timing: automatic alignment".</returns>
    public static string Origin(LyricsDto lyrics) => lyrics.Origin switch
    {
        LyricsOriginDto.Aligned => "Timing: automatic alignment",
        LyricsOriginDto.Interpolated => "Timing: estimated from the line timing",
        _ => "Timing: from the lyrics source",
    };

    /// <summary>Why the mixer shows no banner (<c>karaoke-unavailable</c>).</summary>
    /// <param name="lyrics">The lyrics.</param>
    /// <returns>The readable reason.</returns>
    public static string MixerNotice(LyricsDto lyrics) => lyrics.Status switch
    {
        LyricsStatusDto.None or LyricsStatusDto.Searching => "Searching for the lyrics of this song...",
        LyricsStatusDto.Instrumental => "This song is instrumental, so there are no lyrics to show.",
        LyricsStatusDto.NotFound => (lyrics.Reason ?? "No lyrics were found online.") + TrackPageHint,
        LyricsStatusDto.AlignPending => "The lyrics have no timing yet. Align them to the vocals on the track page.",
        LyricsStatusDto.Aligning => $"The lyrics are being aligned to the vocals ({Percent(lyrics.Alignment?.Progress)} %).",
        _ => OtherNotice(lyrics),
    };

    /// <summary>The readable status of the lyrics card (<c>lyrics-status</c>).</summary>
    /// <param name="lyrics">The lyrics.</param>
    /// <returns>For example "Found, timed per word".</returns>
    public static string Label(LyricsDto lyrics) => lyrics.Status switch
    {
        LyricsStatusDto.Found => FoundLabel(lyrics),
        LyricsStatusDto.None => "Not searched yet",
        LyricsStatusDto.Searching => "Searching...",
        LyricsStatusDto.NotFound => "Not found",
        LyricsStatusDto.AlignPending => "Waiting for alignment",
        _ => OtherLabel(lyrics.Status),
    };

    /// <summary>The expected alignment time (<c>lyrics-align-estimate</c>): the backend's reason while pending, else the seconds.</summary>
    /// <param name="lyrics">The lyrics.</param>
    /// <returns>For example "Aligning takes about 7 minutes on the CPU.", or null without an estimate.</returns>
    public static string? Estimate(LyricsDto lyrics) =>
        lyrics is { Status: LyricsStatusDto.AlignPending, Reason: { } reason } ? reason
        : lyrics.Alignment?.EstimateSeconds is { } seconds ? $"Aligning takes about {Duration(seconds)}."
        : null;

    private static string FoundLabel(LyricsDto lyrics) => Sync(lyrics) switch
    {
        "aligned" => "Found, aligned to the vocals",
        "line" => "Found, timed per line",
        _ => "Found, timed per word",
    };

    private static string OtherLabel(LyricsStatusDto status) => status switch
    {
        LyricsStatusDto.Aligning => "Aligning to the vocals",
        LyricsStatusDto.Disabled => "Online lookups off",
        _ => status.ToString(),
    };

    private static string Duration(int seconds) => seconds < 90
        ? $"{seconds.ToString(CultureInfo.InvariantCulture)} seconds"
        : $"{((seconds + 59) / 60).ToString(CultureInfo.InvariantCulture)} minutes";

    private static string OtherNotice(LyricsDto lyrics) => lyrics.Status switch
    {
        LyricsStatusDto.Failed => lyrics.Reason ?? "The lyrics could not be retrieved.",
        LyricsStatusDto.Disabled => "Online lookups are switched off." + TrackPageHint,
        LyricsStatusDto.Removed => "You removed the lyrics of this song. Search again or enter them on the track page.",
        _ => "These lyrics have no timing the banner can follow.",
    };

    private static string Percent(int? progress) => (progress ?? 0).ToString(CultureInfo.InvariantCulture);
}
