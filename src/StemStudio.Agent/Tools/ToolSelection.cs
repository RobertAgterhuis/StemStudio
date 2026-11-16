namespace StemStudio.Agent.Tools;

/// <summary>The groups and extra tools one page context offers (tool catalogue 1.0.0 "Selection per request").</summary>
/// <param name="Groups">Groups offered in full.</param>
/// <param name="Extras">Single tools of other groups.</param>
public sealed record PageProfile(IReadOnlyList<ToolGroup> Groups, IReadOnlyList<string> Extras)
{
    /// <summary>True when the profile offers the tool.</summary>
    /// <param name="tool">The tool.</param>
    /// <returns>True when selected.</returns>
    public bool Selects(AgentToolDescriptor tool) => Groups.Contains(tool.Group) || Extras.Contains(tool.Name);
}

/// <summary>Page context to profile mapping of tool catalogue 1.0.0.</summary>
public static class ToolSelection
{
    /// <summary>import, tracks, track, mixes, settings, help and unknown pages: core + tracks + the generation entry points.</summary>
    public static PageProfile Default { get; } = new(
        [ToolGroup.Core, ToolGroup.Tracks],
        ["list_styles", "restyle_track", "generate_song", "get_generation_status", "list_generations"]);

    /// <summary>An open mixer: core + mixer + the track jobs.</summary>
    public static PageProfile Mixer { get; } = new(
        [ToolGroup.Core, ToolGroup.Mixer],
        ["start_detail_separation", "export_audio", "transcribe_stem", "get_lyrics_status", "fetch_lyrics", "align_lyrics", "list_styles", "restyle_track"]);

    /// <summary>The library page: core + library + asset and generation management.</summary>
    public static PageProfile Library { get; } = new(
        [ToolGroup.Core, ToolGroup.Library],
        [
            "export_audio", "transcribe_stem", "retry_track", "delete_mix", "rename_mix", "duplicate_mix", "get_lyrics_status",
            "get_generation_status", "list_generations", "import_take", "cancel_generation", "retry_generation", "delete_generation",
        ]);

    /// <summary>The 009/010 pages: core + create.</summary>
    public static PageProfile Create { get; } = new([ToolGroup.Core, ToolGroup.Create], []);

    /// <summary>Every profile (for the start-up cap check).</summary>
    public static IReadOnlyList<PageProfile> All { get; } = [Default, Mixer, Library, Create];

    /// <summary>The profile of a page name (case-insensitive).</summary>
    /// <param name="page">Page name.</param>
    /// <returns>The profile.</returns>
    public static PageProfile For(string page) => page.Trim().ToLowerInvariant() switch
    {
        "mixer" => Mixer,
        "library" => Library,
        "styles" or "generations" or "lyrics" or "songs" or "voices" => Create,
        _ => Default,
    };
}
