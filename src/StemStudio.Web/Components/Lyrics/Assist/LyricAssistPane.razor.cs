using Microsoft.AspNetCore.Components;
using StemStudio.Contracts.LyricAssist;

namespace StemStudio.Web.Components.Lyrics.Assist;

/// <summary>The assistant pane: composes the status, refusals, toolbar, decisions, list and tools of one session.</summary>
public sealed partial class LyricAssistPane : IDisposable
{
    /// <summary>The editor link (session and selection).</summary>
    [Parameter, EditorRequired]
    public EditorHost Host { get; set; } = default!;

    /// <summary>Moves the text cursor to a suggestion's highlight.</summary>
    [Parameter]
    public EventCallback<LyricSuggestionDto> OnReveal { get; set; }

    /// <summary>Escape in the list: back to the text.</summary>
    [Parameter]
    public EventCallback<LyricSuggestionDto?> OnBackToText { get; set; }

    /// <summary>Inserts a word at the cursor (rhyme finder).</summary>
    [Parameter]
    public EventCallback<string> OnInsert { get; set; }

    private LyricAssistSession Session => Host.Session;

    private bool ActionsDisabled => Session.LanguageReason is not null || Session.Run?.IsActive == true;

    /// <summary><c>data-state</c>: <c>idle</c>, <c>running</c>, <c>done</c> or <c>unavailable</c>.</summary>
    private string State => (Session.LanguageReason, Session.Run) switch
    {
        ({ }, _) => "unavailable",
        (_, { IsActive: true }) => "running",
        (_, { }) => "done",
        _ => Session.Status is { Dictionary.Available: false } ? "unavailable" : "idle",
    };

    /// <inheritdoc />
    public void Dispose() => Session.Changed -= OnSessionChanged;

    /// <inheritdoc />
    protected override void OnInitialized() => Session.Changed += OnSessionChanged;

    private void OnSessionChanged() => _ = InvokeAsync(StateHasChanged);
}
