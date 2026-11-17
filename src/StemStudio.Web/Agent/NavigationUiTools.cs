using Microsoft.AspNetCore.Components;
using StemStudio.Web.Help;

namespace StemStudio.Web.Agent;

/// <summary>
/// The layout's UI tools (tool catalogue 1.0.0, features 001 and 004): <c>open_track</c>, <c>open_mixer</c>,
/// <c>navigate</c>, <c>open_help</c> and <c>search_help</c>; <c>open_library_folder</c> (1.1.0, feature 022) and
/// <c>open_lyrics_editor</c> (1.2.0, feature 023). Navigation goes through <see cref="NavigationManager"/>,
/// so the mixer's unsaved-changes guard asks the user exactly as for a click.
/// </summary>
/// <param name="navigation">Navigates.</param>
/// <param name="help">The help panel.</param>
/// <param name="search">The help search of the help panel.</param>
/// <param name="catalog">The help topics.</param>
public sealed class NavigationUiTools(NavigationManager navigation, HelpContext help, IHelpSearch search, IHelpCatalog catalog) : IAgentUiToolHandler
{
    private const int MaxHelpHits = 5;
    private const int MaxSummary = 300;

    private static readonly Dictionary<string, string> Pages = new(StringComparer.Ordinal)
    {
        ["import"] = string.Empty,
        ["tracks"] = "tracks",
        ["mixes"] = "mixes",
        ["library"] = "library",
        ["styles"] = "styles",
        ["generations"] = "generations",
        ["lyrics"] = "lyrics",
        ["songs"] = "songs/new",
        ["voices"] = "voices",
        ["settings"] = "settings",
    };

    /// <summary>The tool names this handler owns.</summary>
    public static IReadOnlySet<string> Names { get; } = new HashSet<string>(StringComparer.Ordinal) {
        "open_track", "open_mixer", "navigate", "open_help", "search_help", "open_library_folder", "open_lyrics_editor",
    };

    /// <inheritdoc />
    public bool CanRun(string tool) => Names.Contains(tool);

    /// <inheritdoc />
    public async Task<UiToolResult> ExecuteAsync(string tool, UiToolArguments arguments)
    {
        try
        {
            return UiToolResult.Done(await RunAsync(tool, arguments));
        }
        catch (UiToolException ex)
        {
            return UiToolResult.Fail(ex.Message);
        }
    }

    private async Task<object> RunAsync(string tool, UiToolArguments arguments) => tool switch
    {
        "open_track" => Go($"tracks/{arguments.RequiredId("trackId")}"),
        "open_mixer" => OpenMixer(arguments.RequiredId("trackId"), arguments.Id("mixId")),
        "navigate" => Navigate(arguments.RequiredText("page")),
        "open_help" => await OpenHelpAsync(arguments.RequiredText("topicId")),
        "open_library_folder" => OpenFolder(EditorRoutes.LibraryFolder(arguments.Text("folderId"), arguments.Text("query"))),
        "open_lyrics_editor" => Go(EditorRoutes.LyricsEditor(arguments.RequiredId("lyricsId"), arguments.Id("assistId"))),
        _ => SearchHelp(arguments.RequiredText("query")),
    };

    private object Go(string path)
    {
        navigation.NavigateTo(path);
        return new { opened = true };
    }

    private object OpenMixer(Guid trackId, Guid? mixId)
    {
        if (mixId is not { } mix)
        {
            return Go($"tracks/{trackId}/mixer");
        }

        navigation.NavigateTo($"tracks/{trackId}/mixer?mixId={mix}");
        return new { opened = true, mixLoaded = true };
    }

    private object OpenFolder((string? Path, string? Reason) route) =>
        route.Path is { } path ? Go(path) : new { opened = false, reason = route.Reason };

    private object Navigate(string page) => Pages.TryGetValue(page, out var path)
        ? Go(path)
        : new { opened = false, reason = $"There is no page '{page}'." };

    private async Task<object> OpenHelpAsync(string topicId)
    {
        if (catalog.Topic(topicId) is null)
        {
            return new { opened = false, reason = $"There is no help topic '{topicId}'." };
        }

        await help.ShowAsync(topicId);
        return new { opened = true };
    }

    private List<HelpHitView> SearchHelp(string query) => search.Search(query, MaxHelpHits)
        .Select(hit => catalog.Topic(hit.TopicId)!)
        .Select(topic => new HelpHitView(topic.Id, topic.Title, topic.Summary.Length > MaxSummary ? topic.Summary[..MaxSummary] : topic.Summary))
        .ToList();

    private sealed record HelpHitView(string TopicId, string Title, string Summary);
}
