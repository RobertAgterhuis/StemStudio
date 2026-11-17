namespace StemStudio.Contracts.LyricAssist;

/// <summary>Data of the <c>assist.cancelled</c> event.</summary>
public sealed record LyricAssistCancelledEventDto
{
    /// <summary>The assist.</summary>
    public required Guid AssistId { get; init; }
}
