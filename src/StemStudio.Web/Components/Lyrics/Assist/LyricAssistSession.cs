using StemStudio.Contracts.Agent;
using StemStudio.Contracts.LyricAssist;
using StemStudio.Web.Api;

namespace StemStudio.Web.Components.Lyrics.Assist;

/// <summary>What the user asked the assistant for: the kind, an optional line range, a count and an instruction.</summary>
/// <param name="Kind">The kind.</param>
/// <param name="LineFrom">First line, or null for the whole sheet.</param>
/// <param name="LineTo">Last line, or null.</param>
/// <param name="Count">Alternatives, continuation lines or titles, or null for the default.</param>
/// <param name="Instruction">Optional instruction (at most 200 characters).</param>
public sealed record AssistRequest(AssistKindDto Kind, int? LineFrom = null, int? LineTo = null, int? Count = null, string? Instruction = null);

/// <summary>
/// The lyric assistant of one open sheet (feature 023): starts assists only on an explicit user action (FR-002), saves unsaved
/// changes first, asks for the Claude notice before anything is sent (FR-026), follows the event stream and keeps the
/// suggestions, the refusals and the live-region text. Components render it and call its actions; it holds no markup.
/// </summary>
public sealed partial class LyricAssistSession(IStemStudioApiClient api, EditorDocument document)
{
    /// <summary>The open sheet.</summary>
    public EditorDocument Document { get; } = document;

    /// <summary>The suggestions.</summary>
    public SuggestionBoard Board { get; } = new();

    /// <summary>Availability of the layers, or null before <see cref="LoadStatusAsync"/>.</summary>
    public LyricAssistStatusDto? Status { get; private set; }

    /// <summary>The newest assist, or null.</summary>
    public AssistRun? Run { get; private set; }

    /// <summary>The refusal of the last request, or null.</summary>
    public AssistRefusal? Refusal { get; private set; }

    /// <summary>A readable error of the last action, or null.</summary>
    public string? Error { get; private set; }

    /// <summary>The text of the polite live region.</summary>
    public string Live { get; private set; } = string.Empty;

    /// <summary>A request waiting for the Claude notice to be acknowledged, or null.</summary>
    public AssistRequest? PendingNotice { get; private set; }

    /// <summary>The task following the newest assist's events (completed when none runs).</summary>
    public Task Following { get; private set; } = Task.CompletedTask;

    /// <summary>Raised after every state change made outside a user event (stream events).</summary>
    public event Action? Changed;

    /// <summary>Sets the live-region text (the focused suggestion, FR-028).</summary>
    /// <param name="text">The announcement.</param>
    public void Announce(string text) => Live = text;

    /// <summary>Raises <see cref="Changed"/> (after a user action of a pane part).</summary>
    public void NotifyChanged() => Changed?.Invoke();

    /// <summary>True when the model layer can run: a ready backend other than <c>none</c>.</summary>
    public bool ModelReady => Status?.Model is { Ready: true, Backend: not AgentBackendDto.None };

    /// <summary>Why the controls are disabled for this sheet's language, or null (FR-001).</summary>
    public string? LanguageReason => Document.AssistLanguage is not null ? null
        : Document.Sheet.Language == "unknown" ? "Choose the language of the lyrics first: the assistant works on Dutch and English lyrics."
        : "The assistant works on Dutch and English lyrics only.";

    /// <summary>Loads the status (layers, model, Claude notice).</summary>
    /// <returns>A task that completes when loaded; a failure becomes <see cref="Error"/>.</returns>
    public async Task LoadStatusAsync() => (Status, Error) = await ApiCall.TryAsync(() => api.GetLyricAssistStatusAsync());

    /// <summary>Starts an assist: Claude notice first, then save, then <c>POST /api/lyrics/{id}/assists</c> and follow its events.</summary>
    /// <param name="request">The request.</param>
    /// <returns>A task that completes when the assist was created (or refused); the events are followed in <see cref="Following"/>.</returns>
    public async Task StartAsync(AssistRequest request)
    {
        if (Status?.ClaudeNoticeRequired == true)
        {
            PendingNotice = request;
            return;
        }

        if (await SaveIfDirtyAsync())
        {
            await CreateAsync(request);
        }
    }

    /// <summary>Cancels the running assist.</summary>
    /// <returns>A task that completes when the cancel was sent.</returns>
    public async Task CancelAsync()
    {
        if (Run is { IsActive: true } run)
        {
            var (assist, error) = await ApiCall.TryAsync(() => api.CancelLyricAssistAsync(run.AssistId));
            Error = error;
            Take(assist);
        }
    }

    /// <summary>Opens the pane on an existing assist (<c>?assist=</c> of <c>open_lyrics_editor</c>) and follows it while it runs.</summary>
    /// <param name="assistId">The assist.</param>
    /// <returns>A task that completes when the snapshot is loaded.</returns>
    public async Task OpenAsync(Guid assistId)
    {
        var (assist, error) = await ApiCall.TryAsync(() => api.GetLyricAssistAsync(assistId));
        Error = error;
        if (assist is not null && assist.LyricsId == Document.Id)
        {
            Run = AssistRun.From(assist);
            Board.Merge(assist.Suggestions);
            Follow();
        }
    }

    /// <summary>Saves unsaved editor changes (the normal 010 save with the expected revision).</summary>
    /// <returns>True when nothing was unsaved or the save worked.</returns>
    public async Task<bool> SaveIfDirtyAsync()
    {
        if (!Document.IsDirty)
        {
            return true;
        }

        try
        {
            Document.Load(await api.UpdateLyricsSheetAsync(Document.Id, Document.ToInput()));
            return true;
        }
        catch (ApiException ex)
        {
            // A conflict must not reload: that would throw the user's unsaved text away.
            Error = ex.Error.Code == LyricAssistProblemTypes.RevisionConflict
                ? "The lyrics were saved in another tab. Copy your changes, reload the page and paste them again."
                : ex.Error.UserMessage;
            return false;
        }
    }

    private async Task CreateAsync(AssistRequest request)
    {
        try
        {
            var assist = await api.CreateLyricAssistAsync(Document.Id, ToBody(request));
            (Refusal, Error) = (null, null);
            Run = AssistRun.From(assist);
            Board.Merge(assist.Suggestions);
            Live = AssistText.Started(request.Kind, SheetText.Lines(Document.Text).Length);
            Follow();
        }
        catch (ApiException ex)
        {
            await RefuseAsync(request, ex.Error);
        }
    }

    private CreateLyricAssistRequest ToBody(AssistRequest request) => new()
    {
        Kind = request.Kind,
        ExpectedRevision = Document.Revision,
        LineFrom = request.LineFrom,
        LineTo = request.LineTo,
        Count = request.Count,
        Instruction = string.IsNullOrWhiteSpace(request.Instruction) ? null : request.Instruction.Trim(),
    };

    private async Task RefuseAsync(AssistRequest request, ApiError error)
    {
        if (error.Code == LyricAssistProblemTypes.ClaudeNoticeRequired)
        {
            PendingNotice = request;
            return;
        }

        await HandleAsync(error);
    }

    private void Follow()
    {
        if (Run is { IsActive: true } run)
        {
            Following = FollowAsync(run);
        }
    }

    private async Task FollowAsync(AssistRun run)
    {
        try
        {
            await foreach (var e in api.StreamLyricAssistEventsAsync(run.AssistId))
            {
                ApplyEvent(run, e);
            }
        }
        catch (Exception ex) when (ex is ApiException or System.Text.Json.JsonException or IOException or HttpRequestException)
        {
            // The snapshot below has everything the stream missed.
        }

        await ReconcileAsync(run);
    }

    private void ApplyEvent(AssistRun run, LyricAssistEvent e)
    {
        if (e.Type == LyricAssistEventTypes.SuggestionAdded)
        {
            Board.Merge([e.Read<LyricSuggestionDto>()]);
        }

        run.Apply(e);
        Live = e.Type switch
        {
            LyricAssistEventTypes.Completed => AssistText.Finished(run.Kind, run.Counts),
            LyricAssistEventTypes.Failed => run.Failure!.Message,
            LyricAssistEventTypes.Cancelled => $"{AssistText.KindName(run.Kind)} cancelled",
            _ => Live,
        };
        Changed?.Invoke();
    }

    private async Task ReconcileAsync(AssistRun run)
    {
        if (!run.IsActive)
        {
            return;
        }

        var (assist, _) = await ApiCall.TryAsync(() => api.GetLyricAssistAsync(run.AssistId));
        if (assist is not null)
        {
            run.Take(assist);
            Board.Merge(assist.Suggestions);
            Live = run.Status == AssistStatusDto.Completed ? AssistText.Finished(run.Kind, run.Counts) : Live;
            Changed?.Invoke();
        }
    }

    private void Take(LyricAssistDto? assist)
    {
        if (assist is not null && Run?.AssistId == assist.AssistId)
        {
            Run.Take(assist);
            Board.Merge(assist.Suggestions);
        }
    }
}
