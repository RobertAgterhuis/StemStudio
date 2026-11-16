using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.Model;

/// <summary>
/// Which Claude key is in use and why (data-model 3, state table): the result of <see cref="Ports.IClaudeKeyProvider.Resolve"/>.
/// The environment variable wins over a stored key. The domain uses the Contracts enums (as in 011); the generated
/// <c>ToString</c> shows the key as <see cref="ClaudeApiKey.Redacted"/>.
/// </summary>
public sealed record ClaudeKeyResolution
{
    /// <summary>The active key; null when no key is usable.</summary>
    public ClaudeApiKey? Key { get; init; }

    /// <summary>Source of the active key; null when no key is usable.</summary>
    public ClaudeKeySourceDto? Source { get; init; }

    /// <summary>Summary state.</summary>
    public required ClaudeKeyStateDto State { get; init; }

    /// <summary>Last four characters of the active key; null when no key is usable.</summary>
    public string? LastFour { get; init; }

    /// <summary>State of the stored key, independent of the environment variable.</summary>
    public required StoredClaudeKeyStateDto Stored { get; init; }

    /// <summary>Last four characters of the stored key (also when overridden or unreadable).</summary>
    public string? StoredLastFour { get; init; }

    /// <summary>Time of the last save of the stored key.</summary>
    public DateTimeOffset? StoredAt { get; init; }

    /// <summary>False when the key ring directory cannot be used.</summary>
    public required bool StoreAvailable { get; init; }

    /// <summary>Why the store is unavailable (<see cref="KeyStoreUnavailableReasons"/>); null when available.</summary>
    public string? StoreUnavailableReason { get; init; }

    /// <summary>Readable explanation for <c>stored-unreadable</c> or an unavailable store; else null.</summary>
    public string? Reason { get; init; }

    /// <summary>Readable fix, e.g. "Enter the key again."; else null.</summary>
    public string? Fix { get; init; }

    /// <summary>True when a usable key is available.</summary>
    public bool Configured => Key is not null;
}
