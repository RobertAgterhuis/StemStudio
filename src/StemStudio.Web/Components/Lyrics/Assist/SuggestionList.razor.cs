using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using StemStudio.Contracts.LyricAssist;

namespace StemStudio.Web.Components.Lyrics.Assist;

/// <summary>The suggestion list and its keys (FR-028).</summary>
public sealed partial class SuggestionList
{
    /// <summary>Moves the text cursor to a suggestion's highlight.</summary>
    [Parameter]
    public EventCallback<LyricSuggestionDto> OnReveal { get; set; }

    /// <summary>Escape: back to the text at the suggestion's position.</summary>
    [Parameter]
    public EventCallback<LyricSuggestionDto?> OnBackToText { get; set; }

    private LyricSuggestionDto? Active => Session.Board.Active is { } id ? Session.Board.Find(id) : null;

    private void FocusEntry(LyricSuggestionDto s)
    {
        if (Session.Board.Active != s.SuggestionId)
        {
            Session.Board.Focus(s.SuggestionId);
            Session.NotifyChanged();
        }
    }

    private Task OnKeyAsync(KeyboardEventArgs e) => SuggestionKeys.Of(e) switch
    {
        SuggestionKey.Next => MoveAsync(1),
        SuggestionKey.Previous => MoveAsync(-1),
        SuggestionKey.Accept when Active is { } s => DecideAsync(s, x => x.AcceptAsync([s.SuggestionId])),
        SuggestionKey.Reject when Active is { } s => DecideAsync(s, x => x.RejectAsync(s.SuggestionId)),
        SuggestionKey.Intentional when Active is { } s && LyricAssistSession.IsCorrection(s) =>
            DecideAsync(s, x => x.MarkIntentionalAsync(s.SuggestionId, LyricAssistSession.DefaultScope(s))),
        SuggestionKey.Back => OnBackToText.InvokeAsync(Active),
        _ => Task.CompletedTask,
    };

    private async Task DecideAsync(LyricSuggestionDto s, Func<LyricAssistSession, Task> action)
    {
        var index = Session.Board.Visible.ToList().FindIndex(v => v.SuggestionId == s.SuggestionId);
        await RunAsync(action);
        await FollowAsync(s.SuggestionId, index);
    }

    /// <summary>After a decision: focuses the entry that took the decided one's place (none when it is still open or the list is empty).</summary>
    private async Task FollowAsync(Guid decided, int index)
    {
        var visible = Session.Board.Visible;
        if (visible.Count == 0 || visible.Any(v => v.SuggestionId == decided))
        {
            return;
        }

        var next = visible[Math.Min(index, visible.Count - 1)];
        Session.Board.Focus(next.SuggestionId);
        await OnReveal.InvokeAsync(next);
        Session.NotifyChanged();
    }

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
