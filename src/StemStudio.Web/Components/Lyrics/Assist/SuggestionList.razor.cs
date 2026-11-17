using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using StemStudio.Contracts.LyricAssist;

namespace StemStudio.Web.Components.Lyrics.Assist;

/// <summary>The suggestion listbox and its keys (FR-028).</summary>
public sealed partial class SuggestionList
{
    /// <summary>Moves the text cursor to a suggestion's highlight.</summary>
    [Parameter]
    public EventCallback<LyricSuggestionDto> OnReveal { get; set; }

    /// <summary>Escape: back to the text at the suggestion's position.</summary>
    [Parameter]
    public EventCallback<LyricSuggestionDto?> OnBackToText { get; set; }

    /// <summary>The element id of the listbox (focused again after the text cursor moved).</summary>
    public const string ListId = "lyric-assist-listbox";

    private string? ActiveId => Session.Board.Active is { } id ? SuggestionEntry.OptionId(id) : null;

    private LyricSuggestionDto? Active => Session.Board.Active is { } id ? Session.Board.Find(id) : null;

    private void OnFocusAsync()
    {
        if (Session.Board.Active is null && Session.Board.Move(1) is { } s)
        {
            Session.Announce(AssistText.Focused(s));
            Session.NotifyChanged();
        }
    }

    private Task OnKeyAsync(KeyboardEventArgs e) => SuggestionKeys.Of(e) switch
    {
        SuggestionKey.Next => MoveAsync(1),
        SuggestionKey.Previous => MoveAsync(-1),
        SuggestionKey.Accept when Active is { } s => RunAsync(x => x.AcceptAsync([s.SuggestionId])),
        SuggestionKey.Reject when Active is { } s => RunAsync(x => x.RejectAsync(s.SuggestionId)),
        SuggestionKey.Intentional when Active is { } s && LyricAssistSession.IsCorrection(s) =>
            RunAsync(x => x.MarkIntentionalAsync(s.SuggestionId, LyricAssistSession.DefaultScope(s))),
        SuggestionKey.Back => OnBackToText.InvokeAsync(Active),
        _ => Task.CompletedTask,
    };

    private async Task MoveAsync(int step)
    {
        if (Session.Board.Move(step) is { } s)
        {
            await SelectAsync(s);
        }
    }

    private async Task SelectAsync(LyricSuggestionDto s)
    {
        Session.Board.Focus(s.SuggestionId);
        Session.Announce(AssistText.Focused(s));
        await OnReveal.InvokeAsync(s);
        Session.NotifyChanged();
    }
}
