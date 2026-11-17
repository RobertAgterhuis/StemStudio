using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using StemStudio.Contracts;
using StemStudio.Contracts.LyricAssist;
using StemStudio.Web.Api;
using StemStudio.Web.Components.Lyrics.Assist;
using StemStudio.Web.Services;

namespace StemStudio.Web.Components.Songs;

/// <summary>
/// The lyrics editor page (<c>/lyrics/{id}</c>): the 010 fields and save, the text with its highlight layer, and the assistant pane of
/// feature 023 with its keys (F8 / Shift+F8 in the editor, Ctrl+Alt+C to check). Unsaved changes are guarded on navigation.
/// </summary>
public sealed partial class LyricsEditorPage : IAsyncDisposable
{
    private ElementReference _root;
    private ElementReference _text;
    private IAsyncDisposable? _keys;
    private SyllableCounter? _counter;

    [Inject]
    private IStemStudioApiClient Api { get; set; } = default!;

    [Inject]
    private ILyricEditorInterop Editor { get; set; } = default!;

    [Inject]
    private IConfirmService Confirm { get; set; } = default!;

    [Inject]
    private TimeProvider Time { get; set; } = default!;

    /// <summary>The sheet.</summary>
    [Parameter]
    public Guid Id { get; set; }

    /// <summary><c>?assist=</c>: opens the pane on that assist (<c>open_lyrics_editor</c>).</summary>
    [SupplyParameterFromQuery(Name = "assist")]
    public Guid? AssistId { get; set; }

    private EditorDocument? Document => Host?.Session.Document;

    private EditorHost? Host { get; set; }

    private string? LoadError { get; set; }

    private FieldProblem? Problem { get; set; }

    private bool Saving { get; set; }

    private bool PaneOpen { get; set; }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        ReleaseHost();
        if (_keys is not null)
        {
            await _keys.DisposeAsync();
        }
    }

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        if (Document?.Id != Id)
        {
            await LoadAsync();
        }
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_keys is null && Host is not null)
        {
            _keys = await Editor.BindKeysAsync(_root);
        }
    }

    private async Task LoadAsync()
    {
        var (sheet, error) = await ApiCall.TryAsync(() => Api.GetLyricsSheetAsync(Id));
        LoadError = error;
        if (sheet is not null)
        {
            ReleaseHost();
            _counter = new SyllableCounter(Api, Time);
            Host = new EditorHost(new LyricAssistSession(Api, new EditorDocument(sheet)), () => Editor.GetSelectionAsync(_text)) { Syllables = _counter };
            Host.Session.Changed += OnChanged;
            await StartSessionAsync(Host.Session);
        }
    }

    // Another sheet on the same page instance (/lyrics/A -> /lyrics/B) or the page went away: the old session stops following its stream.
    private void ReleaseHost()
    {
        if (Host is not null)
        {
            Host.Session.Changed -= OnChanged;
            Host.Session.Dispose();
        }

        _counter?.Dispose();
    }

    private async Task StartSessionAsync(LyricAssistSession session)
    {
        await session.LoadStatusAsync();
        await session.GuessLanguageAsync();
        if (AssistId is { } assist)
        {
            PaneOpen = true;
            await session.OpenAsync(assist);
        }
    }

    private void OnChanged() => _ = InvokeAsync(StateHasChanged);

    private async Task OnTextAsync(ChangeEventArgs args)
    {
        Document!.Text = (string)args.Value!;
        if (await Host!.CountSyllablesAsync())
        {
            StateHasChanged();
        }
    }

    private async Task SaveAsync()
    {
        Problem = Document!.Title.Trim().Length is 0 or > ContractLimits.LyricsTitleMaxLength ? new FieldProblem("title", "Enter a title of 1 to 80 characters.") : null;
        if (Problem is null)
        {
            Saving = true;
            await SendAsync(Document);
            Saving = false;
        }
    }

    private async Task SendAsync(EditorDocument document)
    {
        try
        {
            document.Load(await Api.UpdateLyricsSheetAsync(document.Id, document.ToInput()));
        }
        catch (ApiException ex)
        {
            Problem = FieldProblem.From(ex.Error, conflictField: "text");
        }
    }

    private Task OnKeyAsync(KeyboardEventArgs e) => SuggestionKeys.Of(e) switch
    {
        SuggestionKey.Check when Host is { Session.LanguageReason: null } host => OpenAndCheckAsync(host),
        SuggestionKey.Next => MoveAsync(1),
        SuggestionKey.Previous => MoveAsync(-1),
        _ => Task.CompletedTask,
    };

    private async Task OpenAndCheckAsync(EditorHost host)
    {
        PaneOpen = true;
        await host.RequestAsync(AssistKindDto.Check);
    }

    private async Task MoveAsync(int step)
    {
        if (Host!.Session.Board.Move(step) is { } s)
        {
            PaneOpen = true;
            Host.Session.Announce(AssistText.Focused(s));
            await RevealAsync(s);
        }
    }

    private async Task RevealAsync(LyricSuggestionDto s)
    {
        await SelectAsync(s);
        await Editor.FocusAsync(SuggestionEntry.EntryId(s.SuggestionId));
    }

    private Task BackToTextAsync(LyricSuggestionDto? s) => s is null ? Editor.SelectAsync(_text, 0, 0) : SelectAsync(s);

    private Task SelectAsync(LyricSuggestionDto s)
    {
        var start = SheetText.OffsetOf(Document!.Text, s.Anchor.Line) + s.Anchor.Start;
        return Editor.SelectAsync(_text, start, start + s.Anchor.Length);
    }

    private async Task InsertAsync(string word)
    {
        var (start, end) = await Editor.GetSelectionAsync(_text);
        var text = Document!.Text;
        var (a, b) = (Math.Clamp(start, 0, text.Length), Math.Clamp(end, 0, text.Length));
        Document.Text = string.Concat(text.AsSpan(0, a), word, text.AsSpan(b));
        StateHasChanged();
        await Editor.SelectAsync(_text, a + word.Length, a + word.Length);
    }

    private async Task ConfirmLeaveAsync(LocationChangingContext context)
    {
        if (Document is { IsDirty: true } && !await Confirm.ConfirmAsync("Unsaved lyrics", "Leave without saving your changes?", "Leave"))
        {
            context.PreventNavigation();
        }
    }
}
