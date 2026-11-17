namespace StemStudio.Contracts.LyricAssist;

/// <summary>SSE <c>event:</c> names of <c>GET /api/lyric-assists/{assistId}/events</c> (data-model section 10).</summary>
public static class LyricAssistEventTypes
{
    /// <summary>Data: <see cref="LyricAssistStartedEventDto"/>.</summary>
    public const string Started = "assist.started";

    /// <summary>Data: <see cref="LyricAssistProgressEventDto"/>.</summary>
    public const string Progress = "assist.progress";

    /// <summary>Data: <see cref="LyricSuggestionDto"/>.</summary>
    public const string SuggestionAdded = "suggestion.added";

    /// <summary>Data: <see cref="LyricAssistNoteEventDto"/>.</summary>
    public const string Note = "assist.note";

    /// <summary>Data: <see cref="Agent.TokenUsageDto"/> (Claude only).</summary>
    public const string UsageUpdated = "usage.updated";

    /// <summary>Data: <see cref="LyricAssistCompletedEventDto"/>; terminal.</summary>
    public const string Completed = "assist.completed";

    /// <summary>Data: <see cref="LyricAssistFailedEventDto"/>; terminal.</summary>
    public const string Failed = "assist.failed";

    /// <summary>Data: <see cref="LyricAssistCancelledEventDto"/>; terminal.</summary>
    public const string Cancelled = "assist.cancelled";

    /// <summary>Every event name, in contract order.</summary>
    public static IReadOnlyList<string> All { get; } = [Started, Progress, SuggestionAdded, Note, UsageUpdated, Completed, Failed, Cancelled];

    /// <summary>True for the events after which the stream closes.</summary>
    /// <param name="eventType">An event name.</param>
    /// <returns>True for completed, failed and cancelled.</returns>
    public static bool IsTerminal(string eventType) => eventType is Completed or Failed or Cancelled;
}
