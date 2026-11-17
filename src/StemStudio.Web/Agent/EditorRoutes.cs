using System.Text.RegularExpressions;

namespace StemStudio.Web.Agent;

/// <summary>
/// Addresses of the UI tools <c>open_library_folder</c> (catalogue 1.1.0, feature 022) and <c>open_lyrics_editor</c> (1.2.0, 023).
/// The arguments come from the agent's model: they are checked here again (folder id pattern, query length, exactly one of
/// both) and escaped, so no argument can point the browser anywhere else.
/// </summary>
public static partial class EditorRoutes
{
    /// <summary>Longest library search text (<c>query: string 1..100</c>).</summary>
    public const int MaxQueryLength = 100;

    /// <summary>The library folder address, or the reason why there is none.</summary>
    /// <param name="folderId">The folder id (<c>^(ar|al|so)[0-9a-f]{16}$</c>), or null.</param>
    /// <param name="query">The search text, or null.</param>
    /// <returns>The relative address, or null with a readable reason.</returns>
    public static (string? Path, string? Reason) LibraryFolder(string? folderId, string? query) => (folderId, query) switch
    {
        ({ } id, null) when FolderId().IsMatch(id) => ($"library/{id}", null),
        ({ }, null) => (null, "That is not a library folder id."),
        (null, { Length: > 0 and <= MaxQueryLength } text) => ($"library?q={Uri.EscapeDataString(text)}", null),
        (null, { }) => (null, $"The search text must be 1 to {MaxQueryLength} characters."),
        _ => (null, "Give either a folder or a search text."),
    };

    /// <summary>The lyrics editor address; <paramref name="assistId"/> opens the assistant pane on that assist.</summary>
    /// <param name="lyricsId">The sheet.</param>
    /// <param name="assistId">The assist, or null.</param>
    /// <returns>The relative address.</returns>
    public static string LyricsEditor(Guid lyricsId, Guid? assistId) =>
        assistId is { } assist ? $"lyrics/{lyricsId}?assist={assist}" : $"lyrics/{lyricsId}";

    [GeneratedRegex("^(ar|al|so)[0-9a-f]{16}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex FolderId();
}
