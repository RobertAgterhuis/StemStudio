using StemStudio.Agent.Model;
using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.Ports;

/// <summary>
/// The single source of the Claude key for <c>ClaudeChatClientFactory</c>, <c>ClaudeBackendProbe</c> and
/// <c>AgentStatusService</c> (011 follow-up claude-key, data-model 4): the <c>ANTHROPIC_API_KEY</c> environment variable wins,
/// else the readable stored key. Implemented by the ai-agent (<c>ClaudeKeyProvider</c>, T010) over
/// <see cref="IEnvironmentReader"/> and <see cref="IStoredClaudeKeyReader"/>.
/// </summary>
public interface IClaudeKeyProvider
{
    /// <summary>The key in use and the full state (data-model 3); cached until <see cref="Invalidate"/> or an environment change.</summary>
    /// <returns>The resolution; never throws for a missing or unreadable key.</returns>
    ClaudeKeyResolution Resolve();

    /// <summary>The key of a requested source for an explicit connection test.</summary>
    /// <param name="source">Requested source; <see cref="ClaudeKeyTestSourceDto.Active"/> follows the precedence.</param>
    /// <returns>The key and its source, or null when that source has no usable key.</returns>
    SelectedClaudeKey? KeyFor(ClaudeKeyTestSourceDto source);

    /// <summary>Drops the cached resolution; called by the key endpoints after a store or delete.</summary>
    void Invalidate();
}
