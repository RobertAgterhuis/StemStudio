using StemStudio.Agent.Model;
using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.Ports;

/// <summary>What <see cref="IStoredClaudeKeyReader.Read"/> found: the stored key (when it decrypts) and the state of the store.</summary>
public sealed record StoredClaudeKey
{
    /// <summary>Nothing stored, store usable.</summary>
    public static StoredClaudeKey Absent { get; } = new() { State = StoredClaudeKeyStateDto.Absent, StoreAvailable = true };

    /// <summary>State of the stored key.</summary>
    public required StoredClaudeKeyStateDto State { get; init; }

    /// <summary>The decrypted key when <see cref="State"/> is readable; else null.</summary>
    public ClaudeApiKey? Key { get; init; }

    /// <summary>Last four characters saved with the row (also when unreadable); null when absent.</summary>
    public string? LastFour { get; init; }

    /// <summary>Time of the last save or re-protection; null when absent.</summary>
    public DateTimeOffset? StoredAt { get; init; }

    /// <summary>False when the key ring directory cannot be used (storing answers 503).</summary>
    public required bool StoreAvailable { get; init; }

    /// <summary>Why the store is unavailable (<see cref="KeyStoreUnavailableReasons"/>); null when available.</summary>
    public string? StoreUnavailableReason { get; init; }
}
