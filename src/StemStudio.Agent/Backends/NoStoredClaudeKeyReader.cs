using StemStudio.Agent.Ports;
using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.Backends;

/// <summary>
/// The <see cref="IStoredClaudeKeyReader"/> used when the host registers none (e.g. the evaluation harness): nothing is
/// stored and there is no store, so only <c>ANTHROPIC_API_KEY</c> can configure Claude.
/// </summary>
public sealed class NoStoredClaudeKeyReader : IStoredClaudeKeyReader
{
    private static readonly StoredClaudeKey Nothing = new() { State = StoredClaudeKeyStateDto.Absent, StoreAvailable = false };

    private NoStoredClaudeKeyReader()
    {
    }

    /// <summary>The single instance.</summary>
    public static NoStoredClaudeKeyReader Instance { get; } = new();

    /// <inheritdoc />
    public StoredClaudeKey Read() => Nothing;
}
