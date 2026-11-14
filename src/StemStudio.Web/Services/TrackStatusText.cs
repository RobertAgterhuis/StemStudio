using Radzen;
using StemStudio.Contracts.Tracks;

namespace StemStudio.Web.Services;

/// <summary>Display helpers for <see cref="TrackStatusDto"/>.</summary>
public static class TrackStatusText
{
    /// <summary>True for <c>completed</c> and <c>failed</c> (polling stops).</summary>
    /// <param name="status">The status.</param>
    /// <returns>Whether the status is terminal.</returns>
    public static bool IsTerminal(TrackStatusDto status) =>
        status is TrackStatusDto.Completed or TrackStatusDto.Failed;

    /// <summary>Human-readable label.</summary>
    /// <param name="status">The status.</param>
    /// <returns>The label, e.g. "Separating".</returns>
    public static string Label(TrackStatusDto status) => status.ToString();

    /// <summary>Badge colour for a status.</summary>
    /// <param name="status">The status.</param>
    /// <returns>Success, danger or info.</returns>
    public static BadgeStyle Badge(TrackStatusDto status) => status switch
    {
        TrackStatusDto.Completed => BadgeStyle.Success,
        TrackStatusDto.Failed => BadgeStyle.Danger,
        _ => BadgeStyle.Info,
    };
}
