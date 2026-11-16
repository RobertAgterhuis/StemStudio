namespace StemStudio.Contracts.Agent;

/// <summary>Claude backend state; never contains the key or any part of it.</summary>
public sealed record ClaudeStatusDto
{
    /// <summary>True when the ANTHROPIC_API_KEY environment variable is set (presence only).</summary>
    public required bool KeyConfigured { get; init; }

    /// <summary>True when the user acknowledged the paid-service notice in the settings.</summary>
    public required bool NoticeAcknowledged { get; init; }

    /// <summary>Configured Claude model.</summary>
    public required ClaudeModelDto Model { get; init; }
}
