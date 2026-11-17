using System.Globalization;
using StemStudio.Contracts.LyricAssist;

namespace StemStudio.Web.Components.Lyrics.Assist;

/// <summary>Texts of the own-work override (FR-031, ui-test-ids 1.23.0 <c>lyric-own-work-badge</c>).</summary>
public static class OwnWorkText
{
    /// <summary>"Own work confirmed by Robin on 17 November 2026 (matches Fake Song)".</summary>
    /// <param name="confirmed">The override.</param>
    /// <returns>The text.</returns>
    public static string Badge(OwnWorkOverrideDto confirmed) =>
        $"Own work confirmed by {confirmed.ConfirmedBy} on {confirmed.ConfirmedAt.ToString("d MMMM yyyy", CultureInfo.InvariantCulture)} (matches {confirmed.MatchedTrackTitle})";
}
