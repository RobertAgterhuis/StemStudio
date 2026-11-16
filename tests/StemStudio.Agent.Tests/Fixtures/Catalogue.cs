using StemStudio.Agent.Tools;
using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.Tests.Fixtures;

/// <summary>One row of contracts/tool-catalogue.md 1.0.0 (name, feature, kind, risk, undo, group, availability).</summary>
public sealed record CatalogueEntry(
    string Name, string Feature, ToolKindDto Kind, ToolRiskDto Risk, UndoKindDto Undo, ToolGroup Group, ToolAvailability Availability);

/// <summary>The binding tool catalogue 1.0.0 as test data: the providers must match it exactly.</summary>
public static class Catalogue
{
    private const ToolKindDto S = ToolKindDto.Server;
    private const ToolKindDto U = ToolKindDto.Ui;
    private const ToolKindDto M = ToolKindDto.Meta;
    private const ToolRiskDto Read = ToolRiskDto.Read;
    private const ToolRiskDto Change = ToolRiskDto.Change;
    private const ToolRiskDto Job = ToolRiskDto.Job;
    private const ToolRiskDto Heavy = ToolRiskDto.Heavy;
    private const ToolRiskDto Destructive = ToolRiskDto.Destructive;
    private const UndoKindDto None = UndoKindDto.None;
    private const UndoKindDto Inverse = UndoKindDto.Inverse;
    private const UndoKindDto Compensate = UndoKindDto.Compensate;
    private const ToolAvailability Always = ToolAvailability.Always;
    private const ToolAvailability Mixer = ToolAvailability.Mixer;
    private const ToolAvailability Gen = ToolAvailability.GenerationService;
    private const ToolAvailability Voice = ToolAvailability.VoiceService;

    /// <summary>All 57 tools.</summary>
    public static IReadOnlyList<CatalogueEntry> All { get; } =
    [
        new("set_plan", "011", M, Read, None, ToolGroup.Core, Always),
        new("wait_for_job", "011", M, Read, None, ToolGroup.Core, Always),
        new("search_library", "001", S, Read, None, ToolGroup.Core, Always),
        new("get_track", "001", S, Read, None, ToolGroup.Core, Always),
        new("import_from_url", "001", S, Job, Compensate, ToolGroup.Core, Always),
        new("retry_track", "001", S, Job, None, ToolGroup.Tracks, Always),
        new("delete_track", "001", S, Destructive, None, ToolGroup.Core, Always),
        new("list_mixes", "001", S, Read, None, ToolGroup.Core, Always),
        new("get_mix", "001", S, Read, None, ToolGroup.Core, Always),
        new("save_mix_as", "001", S, Change, Compensate, ToolGroup.Mixer, Mixer),
        new("update_mix", "001", S, Change, Inverse, ToolGroup.Mixer, Mixer),
        new("duplicate_mix", "001", S, Change, Compensate, ToolGroup.Tracks, Always),
        new("rename_mix", "001", S, Change, Inverse, ToolGroup.Tracks, Always),
        new("delete_mix", "001", S, Destructive, None, ToolGroup.Tracks, Always),
        new("open_track", "001", U, Read, None, ToolGroup.Core, Always),
        new("open_mixer", "001", U, Read, None, ToolGroup.Core, Always),
        new("mixer_get_state", "001", U, Read, None, ToolGroup.Mixer, Mixer),
        new("mixer_set_channel", "001", U, Change, Inverse, ToolGroup.Mixer, Mixer),
        new("mixer_set_master", "001", U, Change, Inverse, ToolGroup.Mixer, Mixer),
        new("mixer_reset", "001", U, Change, Inverse, ToolGroup.Mixer, Mixer),
        new("transport", "001", U, Change, None, ToolGroup.Mixer, Mixer),
        new("set_loop", "001", U, Change, Inverse, ToolGroup.Mixer, Mixer),
        new("set_metronome", "001", U, Change, Inverse, ToolGroup.Mixer, Mixer),
        new("start_detail_separation", "002", S, Heavy, Inverse, ToolGroup.Tracks, Always),
        new("cancel_detail_separation", "002", S, Change, None, ToolGroup.Tracks, Always),
        new("get_processing_device", "003", S, Read, None, ToolGroup.Core, Always),
        new("search_help", "004", U, Read, None, ToolGroup.Core, Always),
        new("open_help", "004", U, Read, None, ToolGroup.Core, Always),
        new("navigate", "004", U, Read, None, ToolGroup.Core, Always),
        new("get_artist_info", "005", S, Read, None, ToolGroup.Tracks, Always),
        new("refresh_artwork", "005", S, Job, None, ToolGroup.Tracks, Always),
        new("get_lyrics_status", "006", S, Read, None, ToolGroup.Tracks, Always),
        new("fetch_lyrics", "006", S, Job, Compensate, ToolGroup.Tracks, Always),
        new("align_lyrics", "006", S, Heavy, None, ToolGroup.Tracks, Always),
        new("show_karaoke", "006", U, Change, Inverse, ToolGroup.Mixer, Mixer),
        new("export_audio", "007", S, Job, Compensate, ToolGroup.Tracks, Always),
        new("export_stem_pack", "007", S, Job, Compensate, ToolGroup.Library, Always),
        new("list_exports", "007", S, Read, None, ToolGroup.Library, Always),
        new("delete_export", "007", S, Destructive, None, ToolGroup.Library, Always),
        new("transcribe_stem", "008", S, Heavy, Compensate, ToolGroup.Tracks, Always),
        new("list_scores", "008", S, Read, None, ToolGroup.Library, Always),
        new("delete_score", "008", S, Destructive, None, ToolGroup.Library, Always),
        new("get_generation_status", "009", S, Read, None, ToolGroup.Create, Always),
        new("list_styles", "009", S, Read, None, ToolGroup.Create, Gen),
        new("create_style", "009", S, Change, Compensate, ToolGroup.Create, Gen),
        new("delete_style", "009", S, Destructive, None, ToolGroup.Create, Gen),
        new("restyle_track", "009", S, Heavy, Compensate, ToolGroup.Create, Gen),
        new("list_generations", "009", S, Read, None, ToolGroup.Create, Gen),
        new("cancel_generation", "009", S, Change, None, ToolGroup.Create, Gen),
        new("retry_generation", "009", S, Heavy, Inverse, ToolGroup.Create, Gen),
        new("import_take", "009", S, Job, Compensate, ToolGroup.Create, Gen),
        new("delete_generation", "009", S, Destructive, None, ToolGroup.Create, Gen),
        new("list_lyrics_sheets", "010", S, Read, None, ToolGroup.Create, Gen),
        new("save_lyrics_sheet", "010", S, Change, Compensate, ToolGroup.Create, Gen),
        new("generate_song", "010", S, Heavy, Compensate, ToolGroup.Create, Gen),
        new("list_voice_profiles", "010", S, Read, None, ToolGroup.Create, Voice),
        new("convert_take_voice", "010", S, Heavy, Compensate, ToolGroup.Create, Voice),
    ];

    /// <summary>The entries of one feature.</summary>
    /// <param name="feature">Feature number, e.g. "001".</param>
    /// <returns>The entries.</returns>
    public static IReadOnlyList<CatalogueEntry> Of(string feature) => [.. All.Where(entry => entry.Feature == feature)];

    /// <summary>A descriptor for an entry (no handler, empty schema) for registry tests.</summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The descriptor.</returns>
    public static AgentToolDescriptor Descriptor(CatalogueEntry entry) => new()
    {
        Name = entry.Name,
        Feature = entry.Feature,
        Kind = entry.Kind,
        Risk = entry.Risk,
        UndoKind = entry.Undo,
        Group = entry.Group,
        Availability = entry.Availability,
        Title = entry.Name,
        Description = "Test tool " + entry.Name,
        Schema = ToolSchema.Empty,
    };
}

/// <summary>A provider that returns fixed descriptors.</summary>
/// <param name="descriptors">The descriptors.</param>
public sealed class FixedToolProvider(IEnumerable<AgentToolDescriptor> descriptors) : IAgentToolProvider
{
    private readonly IReadOnlyList<AgentToolDescriptor> _descriptors = [.. descriptors];

    /// <inheritdoc />
    public IEnumerable<AgentToolDescriptor> GetTools() => _descriptors;

    /// <summary>One provider per feature with the catalogue's descriptors.</summary>
    /// <returns>The providers.</returns>
    public static IReadOnlyList<IAgentToolProvider> PerFeature() =>
        [.. Catalogue.All.GroupBy(entry => entry.Feature).Select(group => new FixedToolProvider(group.Select(Catalogue.Descriptor)))];
}
