namespace StemStudio.Contracts.Agent;

/// <summary>
/// Status of the Claude key (GET/PUT/DELETE /api/agent/settings/claude-key and <see cref="ClaudeStatusDto.Key"/>, backend API
/// 1.21.0). Never contains the key: at most its last four characters.
/// </summary>
public sealed record ClaudeKeyStatusDto
{
    /// <summary>The status with no environment variable, nothing stored and a usable key store.</summary>
    public static ClaudeKeyStatusDto NotConfigured { get; } = new()
    {
        Configured = false,
        Source = null,
        State = ClaudeKeyStateDto.NotConfigured,
        LastFour = null,
        Stored = StoredClaudeKeyStateDto.Absent,
        StoredLastFour = null,
        StoredAt = null,
        StoreAvailable = true,
        Reason = null,
        Fix = null,
    };

    /// <summary>True when a usable key is available (same value as <see cref="ClaudeStatusDto.KeyConfigured"/>).</summary>
    public required bool Configured { get; init; }

    /// <summary>Source of the active key; null when no key is usable.</summary>
    public required ClaudeKeySourceDto? Source { get; init; }

    /// <summary>Summary state (data-model 3).</summary>
    public required ClaudeKeyStateDto State { get; init; }

    /// <summary>Last four characters of the active key; null when no key is usable.</summary>
    public required string? LastFour { get; init; }

    /// <summary>State of the stored key, independent of the environment variable.</summary>
    public required StoredClaudeKeyStateDto Stored { get; init; }

    /// <summary>Last four characters of the stored key (also when overridden or unreadable); null when nothing is stored.</summary>
    public required string? StoredLastFour { get; init; }

    /// <summary>Time of the last save of the stored key (UTC); null when nothing is stored.</summary>
    public required DateTimeOffset? StoredAt { get; init; }

    /// <summary>False when the key ring directory cannot be used; storing then answers 503 <c>key-store-unavailable</c>.</summary>
    public required bool StoreAvailable { get; init; }

    /// <summary>Readable explanation for <c>stored-unreadable</c> or an unavailable key store; else null.</summary>
    public required string? Reason { get; init; }

    /// <summary>Readable fix, e.g. "Enter the key again."; else null.</summary>
    public required string? Fix { get; init; }
}
