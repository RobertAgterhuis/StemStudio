namespace StemStudio.Agent.Tools.Providers;

/// <summary>Names of the 023 tools and their group (tool catalogue 1.2.0; names are part of the contract).</summary>
public static class LyricAssistToolNames
{
    /// <summary>The tool group offered in the <c>lyrics</c> page context.</summary>
    public const string Group = "lyric-assist";

    /// <summary>Starts a check or devices assist (job).</summary>
    public const string CheckLyrics = "check_lyrics";

    /// <summary>Starts a writing-help assist (job, +service lyricModel).</summary>
    public const string SuggestLyricLines = "suggest_lyric_lines";

    /// <summary>Lists suggestions without text (read).</summary>
    public const string ListLyricSuggestions = "list_lyric_suggestions";

    /// <summary>Accepts suggestions by id (change, approval always: <c>lyrics-edit</c>).</summary>
    public const string ApplyLyricSuggestions = "apply_lyric_suggestions";

    /// <summary>Marks a suggestion's text as intentional (change, inverse).</summary>
    public const string MarkLyricsIntentional = "mark_lyrics_intentional";

    /// <summary>Rhymes from local data (read).</summary>
    public const string FindRhymes = "find_rhymes";

    /// <summary>Syllables per line, numbers only (read).</summary>
    public const string CountSyllables = "count_syllables";

    /// <summary>Opens the lyrics editor, optionally on an assist (ui).</summary>
    public const string OpenLyricsEditor = "open_lyrics_editor";

    /// <summary>The eight tools in catalogue order.</summary>
    public static IReadOnlyList<string> All { get; } =
        [CheckLyrics, SuggestLyricLines, ListLyricSuggestions, ApplyLyricSuggestions, MarkLyricsIntentional, FindRhymes, CountSyllables, OpenLyricsEditor];
}
