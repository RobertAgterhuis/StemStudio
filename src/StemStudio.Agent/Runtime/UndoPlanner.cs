using StemStudio.Agent.Model;
using StemStudio.Agent.Ports;
using StemStudio.Agent.Tools;
using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.Runtime;

/// <summary>
/// Undo (FR-017, data-model 4.4): one group per run; Undo takes the newest group with available entries and applies them in
/// reverse step order as a new run (a synthetic "Undo" user message) without a model turn, through the same policy, events
/// and step log. Undo runs record no entries of their own, so a second Undo takes the next older group.
/// </summary>
public static class UndoPlanner
{
    /// <summary>The model name of undo runs (no language model is used).</summary>
    public const string UndoModel = "undo";

    /// <summary>The text of the synthetic user message.</summary>
    public const string UndoText = "Undo";

    /// <summary>Reason of an entry whose target was changed after the agent's step.</summary>
    public const string ChangedOutside = "it was changed outside the agent since";

    /// <summary>Reason of an entry whose target no longer exists.</summary>
    public const string Vanished = "it no longer exists";

    /// <summary>True for an undo run.</summary>
    /// <param name="run">The run.</param>
    /// <param name="command">Its user message.</param>
    /// <returns>True when the run is an undo run.</returns>
    public static bool IsUndo(AgentRun run, ConversationMessage command)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(command);
        return run.Model == UndoModel && command.Text == UndoText;
    }

    /// <summary>The newest group with available entries, newest entry first (= reverse step order).</summary>
    /// <param name="undo">Undo store (returns available entries newest first).</param>
    /// <param name="conversationId">Conversation.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The entries, or empty.</returns>
    internal static async Task<IReadOnlyList<UndoEntry>> NewestGroupAsync(IUndoStore undo, Guid conversationId, CancellationToken cancellationToken)
    {
        var available = await undo.GetAvailableAsync(conversationId, cancellationToken);
        return available.Count == 0 ? [] : [.. available.Where(entry => entry.RunId == available[0].RunId)];
    }

    /// <summary>Queues an undo run: the synthetic user message and the run; the host marks it active and executes it.</summary>
    internal static async Task<Guid> PlanAsync(AgentServices services, Guid conversationId, CancellationToken cancellationToken)
    {
        var conversation = await services.Conversations.GetAsync(conversationId, cancellationToken) ?? throw new KeyNotFoundException("The conversation does not exist.");
        if (conversation.ActiveRunId is not null)
        {
            throw new AgentConflictException(AgentProblemTypes.RunActive, "A run is active in this conversation.");
        }

        if ((await NewestGroupAsync(services.Undo, conversationId, cancellationToken)).Count == 0)
        {
            throw new AgentConflictException(AgentProblemTypes.NothingToUndo, "There is nothing to undo.");
        }

        var message = await CommandAsync(services, conversationId, Guid.NewGuid(), cancellationToken);
        await services.Runs.CreateRunAsync(await RunAsync(services, message, cancellationToken), cancellationToken);
        return message.RunId!.Value;
    }

    private static async Task<ConversationMessage> CommandAsync(AgentServices services, Guid conversationId, Guid runId, CancellationToken cancellationToken)
    {
        var messages = await services.Conversations.GetMessagesAsync(conversationId, cancellationToken);
        var message = NewCommand(messages, conversationId, runId, services.Time.GetUtcNow());
        await services.Conversations.AppendMessageAsync(message, cancellationToken);
        return message;
    }

    /// <summary>The synthetic "Undo" message, on the page of the conversation's last command.</summary>
    private static ConversationMessage NewCommand(IReadOnlyList<ConversationMessage> messages, Guid conversationId, Guid runId, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        ConversationId = conversationId,
        Seq = messages.Count == 0 ? 1 : messages.Max(item => item.Seq) + 1,
        Role = MessageRoleDto.User,
        Text = UndoText,
        Context = messages.LastOrDefault(item => item.Role == MessageRoleDto.User)?.Context ?? new PageContextDto { Page = "tracks" },
        RunId = runId,
        CreatedAt = now,
    };

    private static async Task<AgentRun> RunAsync(AgentServices services, ConversationMessage message, CancellationToken cancellationToken) => new()
    {
        Id = message.RunId!.Value,
        ConversationId = message.ConversationId,
        UserMessageId = message.Id,
        Status = AgentRunStatusDto.Queued,
        Backend = (await services.Settings.GetAsync(cancellationToken)).Backend,
        Model = UndoModel,
        Tainted = false,
        ModelTurns = 0,
        ToolCalls = 0,
        InvalidCalls = 0,
        StartedAt = services.Time.GetUtcNow(),
    };
}

/// <summary>The undo entries of a succeeded step (none during an undo run or for tools without undo).</summary>
internal static class UndoRecorder
{
    public static IReadOnlyList<UndoEntry> Entries(RunState state, RunStep step, ToolResult result) =>
        state.IsUndo || step.UndoKind == UndoKindDto.None ? [] : [.. result.AllUndo.Select(action => Entry(state, step, result, action))];

    private static UndoEntry Entry(RunState state, RunStep step, ToolResult result, UndoAction action) => new()
    {
        Id = Guid.NewGuid(),
        ConversationId = state.Run.ConversationId,
        RunId = state.RunId,
        StepId = step.Id,
        Kind = step.UndoKind,
        Action = action,
        Fresh = result.UndoFresh,
        Status = UndoEntryStatus.Available,
    };
}
