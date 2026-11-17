using StemStudio.Contracts.LyricAssist;

namespace StemStudio.Web.Components.Lyrics.Assist;

/// <summary>
/// Links the assistant pane to the editor text: turns a button into a request with the line range of the cursor or the
/// selection (FR-015: alternatives for the line at the cursor, continuation after its block, check and devices for a
/// selected range), and finds the word at the cursor for the rhyme finder.
/// </summary>
/// <param name="session">The assistant session.</param>
/// <param name="selection">Reads the selection of the text area (start and end offsets).</param>
public sealed class EditorHost(LyricAssistSession session, Func<Task<(int Start, int End)>> selection)
{
    /// <summary>The session.</summary>
    public LyricAssistSession Session { get; } = session;

    /// <summary>The syllable counter of the gutter, or null when the page has none.</summary>
    public SyllableCounter? Syllables { get; init; }

    /// <summary>True while the syllable gutter is shown (Advanced).</summary>
    public bool ShowSyllables { get; private set; }

    /// <summary>Shows or hides the gutter; showing counts at once.</summary>
    /// <returns>A task that completes when counted.</returns>
    public async Task ToggleSyllablesAsync()
    {
        ShowSyllables = !ShowSyllables;
        if (ShowSyllables)
        {
            await CountSyllablesAsync(wait: false);
        }
    }

    /// <summary>Counts the syllables of the editor text while the gutter is shown (after a typing pause unless told otherwise).</summary>
    /// <param name="wait">False counts at once.</param>
    /// <returns>True when a new report arrived.</returns>
    public async Task<bool> CountSyllablesAsync(bool wait = true) =>
        ShowSyllables && Syllables is { } counter && Session.Document.AssistLanguage is { } language
        && await counter.CountAsync(language, Session.Document.Text, Session.Document.Assist.RhythmTarget, wait);

    /// <summary>Starts an assist of a kind for the cursor or selection.</summary>
    /// <param name="kind">The kind.</param>
    /// <param name="count">Alternatives, continuation lines or titles, or null.</param>
    /// <param name="instruction">The instruction, or null.</param>
    /// <returns>A task that completes when started.</returns>
    public async Task RequestAsync(AssistKindDto kind, int? count = null, string? instruction = null)
    {
        var (from, to) = Range(kind, Session.Document.Text, await selection());
        await Session.StartAsync(new AssistRequest(kind, from, to, kind is AssistKindDto.Check or AssistKindDto.Devices ? null : count, instruction));
    }

    /// <summary>The word at the cursor, or null.</summary>
    /// <returns>The word.</returns>
    public async Task<string?> WordAtCursorAsync() => SheetText.WordAt(Session.Document.Text, (await selection()).Start);

    /// <summary>The selected text and whether it is exactly one whole line.</summary>
    /// <returns>The text and the flag.</returns>
    public async Task<(string Text, bool WholeLine)> SelectedTextAsync()
    {
        var text = Session.Document.Text;
        var (start, end) = await selection();
        var (a, b) = (Math.Clamp(Math.Min(start, end), 0, text.Length), Math.Clamp(Math.Max(start, end), 0, text.Length));
        var selected = text[a..b];
        var line = SheetText.Lines(text)[SheetText.LineAt(text, a) - 1];
        return (selected, selected.Trim().Length > 0 && selected.Trim() == line.Trim());
    }

    /// <summary>The line range of a request.</summary>
    /// <param name="kind">The kind.</param>
    /// <param name="text">The editor text.</param>
    /// <param name="selection">The selection.</param>
    /// <returns>First and last line, or nulls for the whole sheet.</returns>
    public static (int? From, int? To) Range(AssistKindDto kind, string text, (int Start, int End) selection)
    {
        var first = SheetText.LineAt(text, Math.Min(selection.Start, selection.End));
        var last = SheetText.LineAt(text, Math.Max(selection.Start, selection.End));
        return kind switch
        {
            AssistKindDto.Alternatives => (first, first),
            AssistKindDto.Continue => (first, SheetText.BlockEnd(text, last)),
            AssistKindDto.Titles => (null, null),
            _ when selection.Start == selection.End => (null, null),
            _ => (first, last),
        };
    }
}
