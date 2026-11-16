namespace StemStudio.Contracts.Agent;

/// <summary>Claude backend state; never contains the key, at most its last four characters (backend API 1.21.0).</summary>
public sealed record ClaudeStatusDto
{
    /// <summary>True when a usable key is available: the ANTHROPIC_API_KEY environment variable or (1.21.0) a readable stored key.</summary>
    public required bool KeyConfigured { get; init; }

    /// <summary>True when the user acknowledged the paid-service notice in the settings.</summary>
    public required bool NoticeAcknowledged { get; init; }

    /// <summary>Configured Claude model.</summary>
    public required ClaudeModelDto Model { get; init; }

    /// <summary>
    /// Sources, state and last four characters of the key (1.21.0, required on the wire). Defaults to
    /// <see cref="ClaudeKeyStatusDto.NotConfigured"/> so 1.10.0 call sites keep compiling; the status service fills it.
    /// </summary>
    public ClaudeKeyStatusDto Key { get; init; } = ClaudeKeyStatusDto.NotConfigured;
}
