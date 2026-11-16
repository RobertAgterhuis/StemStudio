using Microsoft.AspNetCore.Components;
using StemStudio.Contracts.Agent;
using StemStudio.Web.Api;

namespace StemStudio.Web.Agent;

/// <summary>
/// Circuit-wide state of the composer agent panel (US1, FR-001/FR-004/FR-007): open or closed, backend status,
/// conversations, the open conversation's messages and the followed run. The panel components only render it;
/// every call and every stream event goes through here and raises <see cref="Changed"/>.
/// </summary>
/// <param name="api">The agent API.</param>
/// <param name="navigation">Gives the page context of a command.</param>
/// <param name="time">Stamps the user's message until the stored one is loaded.</param>
public sealed partial class AgentPanelState(IAgentApiClient api, NavigationManager navigation, TimeProvider time) : IDisposable
{
    private const string LostConnection = "The connection to the agent run was lost. Reopen the conversation to see its state.";
    private CancellationTokenSource _follow = new();

    /// <summary>Raised after every change.</summary>
    public event Action? Changed;

    /// <summary>True while the panel is shown.</summary>
    public bool IsOpen { get; private set; }

    /// <summary>The last backend status, or null before the first load.</summary>
    public AgentStatusDto? Status { get; private set; }

    /// <summary>True when the selected backend can take a command now.</summary>
    public bool IsReady => Status?.Ready == true;

    /// <summary>The conversations, newest first.</summary>
    public IReadOnlyList<ConversationSummaryDto> Conversations { get; private set; } = [];

    /// <summary>The open conversation, or null before the first command.</summary>
    public Guid? ConversationId { get; private set; }

    /// <summary>The messages of the open conversation in order.</summary>
    public IReadOnlyList<AgentMessageDto> Messages { get; private set; } = [];

    /// <summary>The run shown below the messages (the last one started or resumed), or null.</summary>
    public AgentRunView? Run { get; private set; }

    /// <summary>True while the shown run has not ended.</summary>
    public bool IsRunActive => Run is { Ended: false };

    /// <summary>The readable error of the last panel call, or null.</summary>
    public string? Error { get; private set; }

    /// <summary>The backend the badge shows (the run's, else the status').</summary>
    public AgentBackendDto? Backend => Run?.Backend ?? Status?.Backend;

    /// <summary>The model the badge shows.</summary>
    public string? Model => Run?.Model ?? Status?.ActiveModel;

    /// <summary>True when the badge shows the fallback model.</summary>
    public bool UsingFallback => Run?.Backend is not null ? Run.UsingFallback : Status?.UsingFallback == true;

    /// <summary>The stream consumer of the followed run (tests await it).</summary>
    internal Task Following { get; private set; } = Task.CompletedTask;

    /// <summary>Opens or closes the panel.</summary>
    public void Toggle()
    {
        IsOpen = !IsOpen;
        Notify();
    }

    /// <summary>Closes the panel; a running run continues on the server.</summary>
    public void Close()
    {
        IsOpen = false;
        Notify();
    }

    /// <summary>Loads the backend status and the conversation list.</summary>
    /// <returns>A task that completes when both are loaded (or failed with <see cref="Error"/>).</returns>
    public async Task LoadAsync()
    {
        Error = await ApiCall.RunAsync(async () =>
        {
            Status = await api.GetStatusAsync();
            Conversations = await api.ListConversationsAsync();
        });
        Notify();
    }

    /// <summary>True when <paramref name="text"/> can be sent now.</summary>
    /// <param name="text">The input.</param>
    /// <returns>Ready, no active run and text that is not blank.</returns>
    public bool CanSend(string? text) => IsReady && !IsRunActive && !string.IsNullOrWhiteSpace(text);

    /// <summary>Sends a command with the current page context and follows its run.</summary>
    /// <param name="text">The command (1-4,000 characters).</param>
    /// <returns>True when the backend accepted it.</returns>
    public async Task<bool> SendAsync(string text)
    {
        if (!CanSend(text))
        {
            return false;
        }

        var context = AgentPageContexts.From(new Uri(navigation.Uri), new Uri(navigation.BaseUri));
        Error = await ApiCall.RunAsync(() => PostAsync(text, context));
        Notify();
        return Error is null;
    }

    /// <summary>Cancels the followed run (the jobs it started keep running).</summary>
    /// <returns>A task that completes when the API accepted the cancel.</returns>
    public async Task CancelAsync()
    {
        if (Run is { Ended: false } run)
        {
            Error = await ApiCall.RunAsync(() => api.CancelRunAsync(run.RunId, false));
            Notify();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _follow.Cancel();
        _follow.Dispose();
    }

    private async Task PostAsync(string text, PageContextDto context)
    {
        var conversationId = ConversationId ?? await CreateConversationAsync();
        var accepted = await api.SendMessageAsync(conversationId, new SendAgentMessageRequest { Text = text, Context = context });
        Messages = [.. Messages, UserMessage(accepted, text, context)];
        Follow(conversationId, AgentRunView.Start(accepted.RunId), null);
    }

    private AgentMessageDto UserMessage(AgentMessageAcceptedDto accepted, string text, PageContextDto context) => new()
    {
        MessageId = accepted.MessageId,
        Seq = Messages.Count == 0 ? 1 : Messages.Max(m => m.Seq) + 1,
        Role = MessageRoleDto.User,
        Text = text,
        Context = context,
        RunId = accepted.RunId,
        CreatedAt = time.GetUtcNow(),
    };

    private void Follow(Guid conversationId, AgentRunView run, int? after)
    {
        StopFollowing();
        Run = run;
        Following = FollowAsync(new FollowedRun(conversationId, run.RunId, after), _follow.Token);
    }

    private void StopFollowing()
    {
        _follow.Cancel();
        _follow.Dispose();
        _follow = new CancellationTokenSource();
    }

    private async Task FollowAsync(FollowedRun followed, CancellationToken ct)
    {
        try
        {
            await foreach (var e in api.StreamRunEventsAsync(followed.RunId, followed.After, ct))
            {
                await ApplyAsync(followed.ConversationId, e);
            }
        }
        catch (ApiException)
        {
            await RecoverAsync(followed.RunId);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // The panel switched to another conversation; the run continues on the server.
        }
    }

    private async Task ApplyAsync(Guid conversationId, AgentEvent e)
    {
        Run = Run?.Apply(e);
        if (e.IsTerminal)
        {
            await ReloadAsync(conversationId);
        }

        Notify();
    }

    private async Task RecoverAsync(Guid runId)
    {
        var (snapshot, _) = await ApiCall.TryAsync(() => api.GetRunAsync(runId));
        Run = snapshot is null ? Run : AgentRunView.FromSnapshot(snapshot);
        Error = snapshot is null || Run?.Ended != true ? LostConnection : null;
        Notify();
    }

    private void Notify() => Changed?.Invoke();

    private sealed record FollowedRun(Guid ConversationId, Guid RunId, int? After);
}
