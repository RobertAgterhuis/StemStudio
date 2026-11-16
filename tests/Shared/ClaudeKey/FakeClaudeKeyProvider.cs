using StemStudio.Agent.Model;
using StemStudio.Agent.Ports;
using StemStudio.Contracts.Agent;

namespace StemStudio.TestDoubles.ClaudeKey;

/// <summary>
/// Test double of <see cref="IClaudeKeyProvider"/> (011 follow-up claude-key, T005): returns a settable resolution and per-source
/// selections, and counts calls. Used by the backend tests (endpoints, status) instead of the ai-agent's <c>ClaudeKeyProvider</c>.
/// </summary>
public sealed class FakeClaudeKeyProvider : IClaudeKeyProvider
{
    private readonly Dictionary<ClaudeKeyTestSourceDto, SelectedClaudeKey> _selections = [];

    /// <summary>No key: nothing in the environment, nothing stored, store usable.</summary>
    public static ClaudeKeyResolution NotConfigured { get; } = new()
    {
        State = ClaudeKeyStateDto.NotConfigured,
        Stored = StoredClaudeKeyStateDto.Absent,
        StoreAvailable = true,
    };

    /// <summary>What <see cref="Resolve"/> returns; <see cref="NotConfigured"/> until set.</summary>
    public ClaudeKeyResolution Resolution { get; set; } = NotConfigured;

    /// <summary>Number of <see cref="Resolve"/> calls.</summary>
    public int ResolveCount { get; private set; }

    /// <summary>Number of <see cref="Invalidate"/> calls.</summary>
    public int InvalidateCount { get; private set; }

    /// <summary>Sources passed to <see cref="KeyFor"/>, in order.</summary>
    public List<ClaudeKeyTestSourceDto> Selected { get; } = [];

    /// <summary>Makes <see cref="KeyFor"/> return <paramref name="key"/> for <paramref name="requested"/>.</summary>
    /// <param name="requested">Requested test source.</param>
    /// <param name="key">Key and its source to return.</param>
    /// <returns>This fake.</returns>
    public FakeClaudeKeyProvider WithSelection(ClaudeKeyTestSourceDto requested, SelectedClaudeKey key)
    {
        _selections[requested] = key;
        return this;
    }

    /// <inheritdoc />
    public ClaudeKeyResolution Resolve()
    {
        ResolveCount++;
        return Resolution;
    }

    /// <inheritdoc />
    public SelectedClaudeKey? KeyFor(ClaudeKeyTestSourceDto source)
    {
        Selected.Add(source);
        return _selections.GetValueOrDefault(source);
    }

    /// <inheritdoc />
    public void Invalidate() => InvalidateCount++;
}
