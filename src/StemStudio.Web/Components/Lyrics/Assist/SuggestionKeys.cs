using Microsoft.AspNetCore.Components.Web;

namespace StemStudio.Web.Components.Lyrics.Assist;

/// <summary>The assistant keys of ui-test-ids 1.23.0 "Keyboard" (FR-028).</summary>
public enum SuggestionKey
{
    /// <summary>Any other key.</summary>
    None,

    /// <summary>F8: next open suggestion.</summary>
    Next,

    /// <summary>Shift+F8: previous open suggestion.</summary>
    Previous,

    /// <summary>Alt+Enter: accept the focused suggestion.</summary>
    Accept,

    /// <summary>Alt+Backspace: reject it.</summary>
    Reject,

    /// <summary>Alt+I: mark the focused correction as intentional.</summary>
    Intentional,

    /// <summary>Escape: back to the text.</summary>
    Back,

    /// <summary>Ctrl+Alt+C: Check.</summary>
    Check,
}

/// <summary>Maps a key event to a <see cref="SuggestionKey"/>.</summary>
public static class SuggestionKeys
{
    /// <summary>The assistant key of an event.</summary>
    /// <param name="e">The event.</param>
    /// <returns>The key.</returns>
    public static SuggestionKey Of(KeyboardEventArgs e) => (e.Key, e.AltKey, e.CtrlKey, e.ShiftKey) switch
    {
        ("F8", _, _, false) => SuggestionKey.Next,
        ("F8", _, _, true) => SuggestionKey.Previous,
        ("Enter", true, false, _) => SuggestionKey.Accept,
        ("Backspace", true, false, _) => SuggestionKey.Reject,
        ("i" or "I", true, false, _) => SuggestionKey.Intentional,
        ("Escape", _, _, _) => SuggestionKey.Back,
        ("c" or "C", true, true, _) => SuggestionKey.Check,
        _ => SuggestionKey.None,
    };
}
