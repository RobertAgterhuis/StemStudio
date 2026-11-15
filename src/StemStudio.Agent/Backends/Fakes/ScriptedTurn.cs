using System.Text.Json;

namespace StemStudio.Agent.Backends.Fakes;

/// <summary>One scripted assistant turn (contracts/agent-script.schema.json, <c>turns[i]</c>).</summary>
public sealed record ScriptedTurn
{
    /// <summary>Assistant text streamed before the calls, when any.</summary>
    public string? Text { get; init; }

    /// <summary>Tool calls of the turn, in order (parallel calls when more than one).</summary>
    public IReadOnlyList<ScriptedCall> Calls { get; init; } = [];

    /// <summary>Usage reported at the end of the turn, when any.</summary>
    public ScriptedUsage? Usage { get; init; }
}

/// <summary>One scripted tool call.</summary>
public sealed record ScriptedCall
{
    /// <summary>Tool name (may be unknown on purpose, to test rejection).</summary>
    public required string Name { get; init; }

    /// <summary>Arguments object; null means no arguments.</summary>
    public JsonElement? Arguments { get; init; }
}

/// <summary>Scripted token usage of a turn.</summary>
public sealed record ScriptedUsage
{
    /// <summary>Input tokens.</summary>
    public long InputTokens { get; init; }

    /// <summary>Output tokens.</summary>
    public long OutputTokens { get; init; }

    /// <summary>Cached input tokens.</summary>
    public long CachedInputTokens { get; init; }
}

/// <summary>A whole script file (e2e/agent-scripts/*.json).</summary>
public sealed record ChatScript
{
    /// <summary>Format version; 1 for this contract.</summary>
    public int Version { get; init; } = 1;

    /// <summary>Turns answered in order, one per model request.</summary>
    public IReadOnlyList<ScriptedTurn> Turns { get; init; } = [];
}
