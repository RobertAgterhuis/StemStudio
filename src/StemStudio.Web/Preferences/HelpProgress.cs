namespace StemStudio.Web.Preferences;

/// <summary>Help progress (data-model.md 1.1 <c>help</c>).</summary>
/// <param name="WelcomeDismissed">True after a level choice or "Not now" in the welcome panel.</param>
/// <param name="Tours">Tour id to record (at most 50 entries).</param>
/// <param name="HintsDismissed">Dismissed inline-hint topic ids (at most 200 entries).</param>
public sealed record HelpProgress(
    bool WelcomeDismissed,
    IReadOnlyDictionary<string, TourRecord> Tours,
    IReadOnlySet<string> HintsDismissed)
{
    /// <summary>No progress: welcome not dismissed, no tours, no dismissed hints.</summary>
    public static HelpProgress Empty { get; } = new(
        false,
        new Dictionary<string, TourRecord>(StringComparer.Ordinal),
        new HashSet<string>(StringComparer.Ordinal));
}
