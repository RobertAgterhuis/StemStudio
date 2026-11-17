namespace StemStudio.Contracts.LyricAssist;

/// <summary>Data of the <c>assist.completed</c> event.</summary>
public sealed record LyricAssistCompletedEventDto
{
    /// <summary>Shown suggestions per category.</summary>
    public required CategoryCountsDto Counts { get; init; }

    /// <summary>Discarded model items per reason.</summary>
    public required DiscardedCountsDto Discarded { get; init; }

    /// <summary>Duration in milliseconds.</summary>
    public required long DurationMs { get; init; }
}
