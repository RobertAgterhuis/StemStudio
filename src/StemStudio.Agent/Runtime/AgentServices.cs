using StemStudio.Agent.Ports;
using StemStudio.Agent.Tools;

namespace StemStudio.Agent.Runtime;

/// <summary>The ports and services the runner works with (one parameter object keeps every constructor small).</summary>
public sealed record AgentServices
{
    /// <summary>Runs, steps and approvals.</summary>
    public required IRunStore Runs { get; init; }

    /// <summary>Conversations and messages.</summary>
    public required IConversationStore Conversations { get; init; }

    /// <summary>Agent settings (limits).</summary>
    public required IAgentSettingsStore Settings { get; init; }

    /// <summary>Event log and SSE broadcast.</summary>
    public required IRunEventSink Events { get; init; }

    /// <summary>StemStudio's public API over loopback.</summary>
    public required IStemStudioActions Actions { get; init; }

    /// <summary>UI tool round trip.</summary>
    public required IUiToolGateway Ui { get; init; }

    /// <summary>Tool registry.</summary>
    public required AgentToolRegistry Registry { get; init; }

    /// <summary>Chat client per turn.</summary>
    public required IAgentChatSession Chat { get; init; }

    /// <summary>Undo entries.</summary>
    public required IUndoStore Undo { get; init; }

    /// <summary>Clock (timeouts, expiry, job polling).</summary>
    public TimeProvider Time { get; init; } = TimeProvider.System;
}
