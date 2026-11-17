namespace StemStudio.Contracts.LyricAssist;

/// <summary>Body of <c>PUT /api/lyric-assist/settings</c>.</summary>
public sealed record LyricAssistSettingsUpdate
{
    /// <summary>True acknowledges the current notice version; false withdraws the acknowledgement.</summary>
    public required bool AcknowledgeClaudeNotice { get; init; }
}
