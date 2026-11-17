namespace StemStudio.Contracts.LyricAssist;

/// <summary>Lyric-assistant settings (schema <c>LyricAssistSettings</c>): the Claude notice and the own-work statement.</summary>
public sealed record LyricAssistSettingsDto
{
    /// <summary>True when the current Claude notice version is acknowledged.</summary>
    public required bool ClaudeNoticeAcknowledged { get; init; }

    /// <summary>When the notice was acknowledged.</summary>
    public DateTimeOffset? ClaudeNoticeAcknowledgedAt { get; init; }

    /// <summary>Current notice version (<see cref="LyricAssistLimits.ClaudeNoticeVersion"/>).</summary>
    public required string ClaudeNoticeVersion { get; init; }

    /// <summary>Notice text (at most 1,000 characters).</summary>
    public required string ClaudeNoticeText { get; init; }

    /// <summary>Current own-work statement version (<see cref="LyricAssistLimits.OwnWorkStatementVersion"/>).</summary>
    public required string OwnWorkStatementVersion { get; init; }

    /// <summary>Own-work statement text (at most 1,000 characters).</summary>
    public required string OwnWorkStatementText { get; init; }
}
