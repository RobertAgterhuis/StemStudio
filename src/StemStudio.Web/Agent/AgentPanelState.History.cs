using StemStudio.Contracts.Agent;
using StemStudio.Web.Api;

namespace StemStudio.Web.Agent;

/// <summary>
/// History (US5, FR-002, FR-017): undo of the last agent action group, rename and delete of conversations, and the
/// restore of a conversation's last run when it is opened again (a running run's stream is resumed).
/// </summary>
public sealed partial class AgentPanelState
{
    private IReadOnlyList<AgentRunSummaryDto> _runs = [];
    private bool _nothingToUndo;

    /// <summary>The text of the synthetic user message of an undo run (agent <c>UndoPlanner.UndoText</c>).</summary>
    private const string UndoCommand = "Undo";

    /// <summary>
    /// True when the open conversation has a finished run, not every command was undone already (one undo takes one
    /// command group) and the API has not said "nothing to undo".
    /// </summary>
    public bool CanUndo => ConversationId is not null && !IsRunActive && !_nothingToUndo && CommandsLeftToUndo > 0 && (Run is { Ended: true } || _runs.Any(r => IsEnded(r.Status)));

    private int CommandsLeftToUndo =>
        Messages.Where(m => m.Role == MessageRoleDto.User).Sum(m => m.Text == UndoCommand ? -1 : 1);

    /// <summary>True while the conversation's run is active (its delete is disabled).</summary>
    /// <param name="conversation">The conversation.</param>
    /// <returns>Whether a run is active.</returns>
    public bool IsActive(ConversationSummaryDto conversation) =>
        (conversation.ConversationId == ConversationId && IsRunActive) || conversation.LastRunStatus is { } status && !IsEnded(status);

    /// <summary>Undoes the last agent action group of the open conversation and follows the undo run.</summary>
    /// <returns>A task that completes when the undo run is accepted.</returns>
    public async Task UndoAsync()
    {
        if (!CanUndo)
        {
            return;
        }

        var conversationId = ConversationId!.Value;
        try
        {
            var accepted = await api.UndoAsync(conversationId);
            Error = null;
            Follow(new FollowedRun(conversationId, accepted.RunId, null), AgentRunView.Start(accepted.RunId));
        }
        catch (ApiException ex)
        {
            Error = ex.Error.UserMessage;
            _nothingToUndo = ex.Error.IsConflict;
        }

        Notify();
    }

    /// <summary>Renames a conversation.</summary>
    /// <param name="conversationId">The conversation.</param>
    /// <param name="title">The new title (1-120 characters).</param>
    /// <returns>A task that completes when renamed.</returns>
    public async Task RenameConversationAsync(Guid conversationId, string title)
    {
        Error = await ApiCall.RunAsync(async () =>
        {
            var renamed = await api.RenameConversationAsync(conversationId, title);
            Conversations = [.. Conversations.Select(c => c.ConversationId == conversationId ? renamed : c)];
        });
        Notify();
    }

    /// <summary>Deletes a conversation (the caller asked for confirmation); the open one is cleared.</summary>
    /// <param name="conversationId">The conversation.</param>
    /// <returns>A task that completes when deleted.</returns>
    public async Task DeleteConversationAsync(Guid conversationId)
    {
        Error = await ApiCall.RunAsync(async () =>
        {
            await api.DeleteConversationAsync(conversationId);
            Conversations = [.. Conversations.Where(c => c.ConversationId != conversationId)];
            if (conversationId == ConversationId)
            {
                ShowEmpty();
                ConversationId = null;
            }
        });
        Notify();
    }

    private static bool IsEnded(AgentRunStatusDto status) =>
        status is AgentRunStatusDto.Completed or AgentRunStatusDto.Failed or AgentRunStatusDto.Cancelled or AgentRunStatusDto.Interrupted;

    // A finished run is shown from its snapshot; a running one is replayed from its first event (the snapshot has no
    // streamed text), without executing again the UI tools the run no longer waits on.
    private async Task RestoreRunAsync(Guid conversationId, Guid? runId)
    {
        if (runId is not { } id)
        {
            return;
        }

        var snapshot = await api.GetRunAsync(id);
        var view = AgentRunView.FromSnapshot(snapshot);
        if (view.Ended)
        {
            Run = view;
            return;
        }

        var answered = snapshot.Steps.Where(s => s.Status != AgentStepStatusDto.AwaitingUi).Select(s => s.StepId).ToHashSet();
        Follow(new FollowedRun(conversationId, id, null, answered), AgentRunView.Start(id) with { Backend = view.Backend, Model = view.Model });
    }
}
