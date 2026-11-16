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
/// <param name="host">Runs the UI tools the run requests (<c>ui.requested</c>).</param>
public sealed partial class AgentPanelState(IAgentApiClient api, NavigationManager navigation, TimeProvider time, AgentUiToolHost host) : IDisposable
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
        if (Error is null && ConversationId is null && Conversations.Count > 0)
        {
            await SelectConversationAsync(Conversations[0].ConversationId);
            return;
        }

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

        try
        {
            await PostAsync(text, PageContext());
            Error = null;
        }
        catch (ApiException ex)
        {
            Error = ex.Error.UserMessage;
            Status = AgentProblems.StatusOf(ex.Error) ?? Status;
        }

        Notify();
        return Error is null;
    }

    /// <summary>"Switch to local" after a Claude failure: selects Ollama (never silently, FR-020) and reloads the status.</summary>
    /// <returns>A task that completes when saved and reloaded.</returns>
    public async Task SwitchToLocalAsync()
    {
        Error = await ApiCall.RunAsync(() => api.UpdateSettingsAsync(new UpdateAgentSettingsRequest { Backend = AgentBackendDto.Ollama }));
        if (Error is null)
        {
            await RefreshStatusAsync();
            return;
        }

        Notify();
    }

    /// <summary>Reloads the backend status (every 10 s while the panel is open).</summary>
    /// <returns>A task that completes when the status is loaded.</returns>
    public async Task RefreshStatusAsync()
    {
        var (status, _) = await ApiCall.TryAsync(() => api.GetStatusAsync());
        Status = status ?? Status;
        Notify();
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

    /// <summary>
    /// Decides the pending approval, bound to its arguments hash (FR-015). The card stays until
    /// <c>approval.resolved</c>; a conflict (expired, run ended, arguments changed) closes it with the reason.
    /// </summary>
    /// <param name="decision">Approve or reject.</param>
    /// <param name="scope">Once, the heavy steps of this plan, or null for a rejection.</param>
    /// <returns>A task that completes when the API answered.</returns>
    public async Task DecideAsync(ApprovalDecisionDto decision, ApprovalScopeDto? scope)
    {
        if (Run is not { Approval: { } approval } run)
        {
            return;
        }

        try
        {
            await api.DecideApprovalAsync(run.RunId, approval.StepId, new DecideApprovalRequest { Decision = decision, Scope = scope, ArgumentsHash = approval.ArgumentsHash });
            Error = null;
        }
        catch (ApiException ex)
        {
            Error = ex.Error.UserMessage;
            Run = ex.Error.IsConflict ? Run with { Approval = null } : Run;
        }

        Notify();
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
        _nothingToUndo = false;
        Follow(new FollowedRun(conversationId, accepted.RunId, null), AgentRunView.Start(accepted.RunId));
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

    private void Follow(FollowedRun followed, AgentRunView run)
    {
        StopFollowing();
        Run = run;
        Following = FollowAsync(followed, _follow.Token);
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
                await ApplyAsync(followed, e);
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

    private PageContextDto PageContext()
    {
        var context = AgentPageContexts.From(new Uri(navigation.Uri), new Uri(navigation.BaseUri));
        return context.TrackId is { } trackId && host.OpenMixId(trackId) is { } mixId ? context with { MixId = mixId } : context;
    }

    private async Task ApplyAsync(FollowedRun followed, AgentEvent e)
    {
        Run = Run?.Apply(e);
        if (e.Type == AgentEventTypes.UiRequested && e.Read<UiRequestedEventDto>() is var request && followed.Answered?.Contains(request.StepId) != true)
        {
            await RunUiToolAsync(followed.RunId, request);
        }

        if (e.IsTerminal)
        {
            await ReloadAsync(followed.ConversationId);
        }

        Notify();
    }

    // 409 ui-result-not-expected (another tab answered first, or the 15 s passed) needs no message: the step shows it.
    private async Task RunUiToolAsync(Guid runId, UiRequestedEventDto request)
    {
        var result = await host.ExecuteAsync(request);
        await ApiCall.RunAsync(() => api.SubmitUiResultAsync(runId, request.StepId, result));
    }

    private async Task RecoverAsync(Guid runId)
    {
        var (snapshot, _) = await ApiCall.TryAsync(() => api.GetRunAsync(runId));
        Run = snapshot is null ? Run : AgentRunView.FromSnapshot(snapshot);
        Error = snapshot is null || Run?.Ended != true ? LostConnection : null;
        Notify();
    }

    private void Notify() => Changed?.Invoke();

    /// <summary>A followed run; <paramref name="Answered"/> lists the steps a resumed run no longer waits on (their
    /// replayed <c>ui.requested</c> events are not executed again).</summary>
    private sealed record FollowedRun(Guid ConversationId, Guid RunId, int? After, IReadOnlySet<Guid>? Answered = null);
}
