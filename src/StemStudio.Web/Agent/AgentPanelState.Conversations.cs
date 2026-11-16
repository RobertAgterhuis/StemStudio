using StemStudio.Web.Api;

namespace StemStudio.Web.Agent;

/// <summary>Conversation list, new conversation and switching between conversations (US1, FR-002).</summary>
public sealed partial class AgentPanelState
{
    /// <summary>Starts a new, empty conversation and shows it.</summary>
    /// <returns>A task that completes when it is created.</returns>
    public async Task NewConversationAsync()
    {
        _selection++;
        _creating = true;
        Error = await ApiCall.RunAsync(async () =>
        {
            await CreateConversationAsync();
            ShowEmpty();
        });
        _creating = false;
        Notify();
    }

    /// <summary>Opens a stored conversation with its messages.</summary>
    /// <param name="conversationId">The conversation.</param>
    /// <returns>A task that completes when it is loaded.</returns>
    public Task SelectConversationAsync(Guid conversationId) => OpenAsync(conversationId, ++_selection, () => true);

    /// <summary>
    /// Opens the newest conversation when the panel loads, unless the user started or opened one meanwhile (also while
    /// a new conversation is still being created).
    /// </summary>
    private Task OpenNewestAsync(Guid conversationId)
    {
        var selection = _selection;
        return OpenAsync(conversationId, selection, () => !_creating && ConversationId is null);
    }

    private async Task OpenAsync(Guid conversationId, int selection, Func<bool> stillWanted)
    {
        Error = await ApiCall.RunAsync(async () =>
        {
            var conversation = await api.GetConversationAsync(conversationId);
            if (selection != _selection || !stillWanted())
            {
                return; // the user started or opened another conversation meanwhile
            }

            StopFollowing();
            ConversationId = conversation.ConversationId;
            Messages = conversation.Messages;
            Run = null;
            _runs = conversation.Runs;
            _nothingToUndo = false;
            await RestoreRunAsync(conversation.ConversationId, conversation.Runs.OrderBy(r => r.StartedAt).LastOrDefault()?.RunId);
        });
        Notify();
    }

    /// <summary>Increases with every user choice of a conversation; a load that was overtaken is dropped.</summary>
    private int _selection;

    /// <summary>True while <see cref="NewConversationAsync"/> waits for the new conversation.</summary>
    private bool _creating;

    private async Task<Guid> CreateConversationAsync()
    {
        var conversation = await api.CreateConversationAsync();
        ConversationId = conversation.ConversationId;
        Conversations = [conversation, .. Conversations.Where(c => c.ConversationId != conversation.ConversationId)];
        return conversation.ConversationId;
    }

    private void ShowEmpty()
    {
        StopFollowing();
        Messages = [];
        Run = null;
        _runs = [];
    }

    // After the terminal event: the stored answer replaces the streamed text; the list shows the new state.
    private async Task ReloadAsync(Guid conversationId)
    {
        Error = await ApiCall.RunAsync(async () =>
        {
            Messages = (await api.GetConversationAsync(conversationId)).Messages;
            Conversations = await api.ListConversationsAsync();
        });
    }
}
