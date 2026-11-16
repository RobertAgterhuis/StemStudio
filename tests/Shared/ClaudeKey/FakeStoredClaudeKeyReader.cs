using StemStudio.Agent.Ports;
using StemStudio.Contracts.Agent;

namespace StemStudio.TestDoubles.ClaudeKey;

/// <summary>
/// Test double of <see cref="IStoredClaudeKeyReader"/> (011 follow-up claude-key, T005): returns a settable
/// <see cref="StoredClaudeKey"/> and counts reads. Used by the ai-agent tests (<c>ClaudeKeyProvider</c>) instead of the backend's
/// <c>ProtectedClaudeKeyStore</c>.
/// </summary>
public sealed class FakeStoredClaudeKeyReader : IStoredClaudeKeyReader
{
    /// <summary>What <see cref="Read"/> returns; <see cref="StoredClaudeKey.Absent"/> until set.</summary>
    public StoredClaudeKey Current { get; set; } = StoredClaudeKey.Absent;

    /// <summary>Number of <see cref="Read"/> calls (cache tests).</summary>
    public int ReadCount { get; private set; }

    /// <summary>A reader whose stored key exists but cannot be decrypted.</summary>
    /// <param name="lastFour">Last four characters saved with the row.</param>
    /// <param name="storedAt">Save time.</param>
    /// <returns>The fake.</returns>
    public static FakeStoredClaudeKeyReader Unreadable(string lastFour, DateTimeOffset storedAt) => new()
    {
        Current = new StoredClaudeKey { State = StoredClaudeKeyStateDto.Unreadable, LastFour = lastFour, StoredAt = storedAt, StoreAvailable = true },
    };

    /// <summary>A reader whose key ring directory cannot be used.</summary>
    /// <param name="reason">A code of <c>KeyStoreUnavailableReasons</c>.</param>
    /// <returns>The fake.</returns>
    public static FakeStoredClaudeKeyReader StoreUnavailable(string reason) => new()
    {
        Current = new StoredClaudeKey { State = StoredClaudeKeyStateDto.Absent, StoreAvailable = false, StoreUnavailableReason = reason },
    };

    /// <inheritdoc />
    public StoredClaudeKey Read()
    {
        ReadCount++;
        return Current;
    }
}
