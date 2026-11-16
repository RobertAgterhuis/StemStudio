using StemStudio.Agent.Model;
using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.Ports;

/// <summary>A key picked for an explicit Claude connection test, with its source (reported as <c>keySource</c>).</summary>
/// <param name="Key">The key.</param>
/// <param name="Source">Where it comes from.</param>
public sealed record SelectedClaudeKey(ClaudeApiKey Key, ClaudeKeySourceDto Source);
