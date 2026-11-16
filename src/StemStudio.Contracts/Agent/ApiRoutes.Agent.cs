using System.Globalization;

namespace StemStudio.Contracts;

/// <summary>Routes of the agent endpoints (feature 011, contracts/agent-api.openapi.yaml).</summary>
public static partial class ApiRoutes
{
    /// <summary>Root of the agent endpoints.</summary>
    public const string Agent = "/api/agent";

    /// <summary>GET: readiness of the selected backend.</summary>
    public const string AgentStatus = Agent + "/status";

    /// <summary>GET/PUT: agent settings.</summary>
    public const string AgentSettings = Agent + "/settings";

    /// <summary>POST: connection test of a backend.</summary>
    public const string AgentSettingsTest = AgentSettings + "/test";

    /// <summary>GET (status) / PUT (store, local-only) / DELETE (local-only) the Claude key (1.21.0, write-only).</summary>
    public const string AgentClaudeKey = AgentSettings + "/claude-key";

    /// <summary>GET: registry view of the tools.</summary>
    public const string AgentTools = Agent + "/tools";

    /// <summary>GET (list) / POST (create) conversations.</summary>
    public const string AgentConversations = Agent + "/conversations";

    /// <summary>GET/PATCH/DELETE one conversation.</summary>
    /// <param name="conversationId">Conversation identifier.</param>
    /// <returns>The route.</returns>
    public static string AgentConversation(Guid conversationId) => $"{AgentConversations}/{conversationId}";

    /// <summary>POST: send a command to a conversation.</summary>
    /// <param name="conversationId">Conversation identifier.</param>
    /// <returns>The route.</returns>
    public static string AgentMessages(Guid conversationId) => $"{AgentConversation(conversationId)}/messages";

    /// <summary>POST: undo the newest undoable command of a conversation.</summary>
    /// <param name="conversationId">Conversation identifier.</param>
    /// <returns>The route.</returns>
    public static string AgentUndo(Guid conversationId) => $"{AgentConversation(conversationId)}/undo";

    /// <summary>GET: snapshot of a run.</summary>
    /// <param name="runId">Run identifier.</param>
    /// <returns>The route.</returns>
    public static string AgentRun(Guid runId) => $"{Agent}/runs/{runId}";

    /// <summary>GET: SSE event stream of a run, optionally replaying only events after a sequence number.</summary>
    /// <param name="runId">Run identifier.</param>
    /// <param name="after">Last sequence number already seen.</param>
    /// <returns>The route.</returns>
    public static string AgentRunEvents(Guid runId, int? after = null) =>
        after is null ? $"{AgentRun(runId)}/events" : $"{AgentRun(runId)}/events?after={after.Value.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>POST: approve or reject a step.</summary>
    /// <param name="runId">Run identifier.</param>
    /// <param name="stepId">Step identifier.</param>
    /// <returns>The route.</returns>
    public static string AgentApproval(Guid runId, Guid stepId) => $"{AgentRun(runId)}/approvals/{stepId}";

    /// <summary>POST: result of a UI tool executed by the open page.</summary>
    /// <param name="runId">Run identifier.</param>
    /// <param name="stepId">Step identifier.</param>
    /// <returns>The route.</returns>
    public static string AgentUiResult(Guid runId, Guid stepId) => $"{AgentRun(runId)}/ui-results/{stepId}";

    /// <summary>POST: cancel a run.</summary>
    /// <param name="runId">Run identifier.</param>
    /// <returns>The route.</returns>
    public static string AgentCancel(Guid runId) => $"{AgentRun(runId)}/cancel";
}
