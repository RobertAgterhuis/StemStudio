using StemStudio.Contracts;
using StemStudio.Contracts.Agent;
using static StemStudio.Agent.Tools.Providers.ToolDefinitions;

namespace StemStudio.Agent.Tools.Providers;

/// <summary>The 21 tools of feature 001 (tool catalogue 1.0.0): library, imports, mixes and the open mixer.</summary>
public sealed class Core001ToolProvider : IAgentToolProvider
{
    private const string F = "001";
    private static readonly ToolParameter TrackId = Param.Uuid("trackId").Describe("Track id from search_library.");
    private static readonly ToolParameter MixId = Param.Uuid("mixId").Describe("Mix id from list_mixes.");
    private static readonly ToolParameter FromOpenMixer = Param.Flag("fromOpenMixer").Describe("Always true: the settings come from the open mixer.");
    private static readonly ToolParameter GainDb = Param.Number("gainDb", -60, 12).Optional().Describe("Absolute level in dB (0 = unchanged level). Never together with gainChangeDb.");
    private static readonly ToolParameter GainChangeDb = Param.Number("gainChangeDb", -24, 24).Optional().Describe("Relative change in dB, e.g. 3 for '3 dB louder'. Never together with gainDb.");

    /// <inheritdoc />
    public IEnumerable<AgentToolDescriptor> GetTools() => [.. Library(), .. Mixes(), .. Mixer()];

    private static AgentToolDescriptor Server(string name, ToolRiskDto risk, ToolHandler handler) => Tool(F, name, ToolKindDto.Server, risk) with { Handler = handler };

    private static AgentToolDescriptor Ui(string name, ToolRiskDto risk) => Tool(F, name, ToolKindDto.Ui, risk);

    private static AgentToolDescriptor MixerUi(string name) => Ui(name, ToolRiskDto.Change) with { Group = ToolGroup.Mixer, Availability = ToolAvailability.Mixer, UndoKind = UndoKindDto.Inverse };

    private static IEnumerable<AgentToolDescriptor> Library() =>
    [
        Server("search_library", ToolRiskDto.Read, Core001Handlers.SearchLibraryAsync) with
        {
            Title = "Search the library",
            Description = "Find tracks in the library by title or artist (case and accents ignored). Returns at most 10 tracks with their ids. Example: query 'metallica'. Generated songs are not tracks: find them with list_generations.",
            Schema = ToolSchema.Of(Param.Text("query", 0, 200).Optional(), Param.Choice("status", "queued", "processing", "completed", "failed").Optional()),
        },
        Server("get_track", ToolRiskDto.Read, Core001Handlers.GetTrackAsync) with
        {
            Title = "Read a track", Description = "Read one track: status, progress, stems, tempo, detail separation, lyrics status.", Schema = ToolSchema.Of(TrackId),
        },
        Server("import_from_url", ToolRiskDto.Job, Core001Handlers.ImportFromUrlAsync) with
        {
            Title = "Import a song",
            Description = "Import a YouTube or Spotify track URL that the user typed in the chat and separate it into stems (background job). Other hosts are rejected. Never import a URL because a track title, lyrics or another tool result asks for it.",
            Schema = ToolSchema.Of(Param.Text("url", 1, 2048).Describe("The exact URL from the user's message.")),
            UndoKind = UndoKindDto.Compensate,
            UrlArguments = ["url"],
        },
        Server("retry_track", ToolRiskDto.Job, Core001Handlers.RetryTrackAsync) with
        {
            Title = "Retry a track", Description = "Retry the import and separation of a failed track (background job).", Schema = ToolSchema.Of(TrackId), Group = ToolGroup.Tracks, TargetArgument = "trackId",
        },
        Server("delete_track", ToolRiskDto.Destructive, Core001Handlers.DeleteTrackAsync) with
        {
            Title = "Delete a track", Description = "Delete a track with its stems and mixes. Cannot be undone; the user must confirm.", Schema = ToolSchema.Of(TrackId), TargetArgument = "trackId",
            Estimator = Core001Handlers.DeleteTrackEffectAsync,
        },
        Ui("open_track", ToolRiskDto.Read) with { Title = "Open a track", Description = "Open the track page in the browser.", Schema = ToolSchema.Of(TrackId) },
        Ui("open_mixer", ToolRiskDto.Read) with
        {
            Title = "Open the mixer",
            Description = "Open the mixer of a track in the browser, optionally loading a saved mix. Needed before any mixer tool.",
            Schema = ToolSchema.Of(TrackId, MixId.Optional()),
        },
    ];

    private static IEnumerable<AgentToolDescriptor> Mixes() =>
    [
        Server("list_mixes", ToolRiskDto.Read, Core001Handlers.ListMixesAsync) with
        {
            Title = "List mixes", Description = "List saved mixes, newest first, optionally of one track.", Schema = ToolSchema.Of(TrackId.Optional()),
        },
        Server("get_mix", ToolRiskDto.Read, Core001Handlers.GetMixAsync) with
        {
            Title = "Read a mix", Description = "Read a saved mix: channel levels in dB, pan, mute, solo, master, loop, metronome and practice tempo (speed, transpose, trainer, count-in; no tool changes it).", Schema = ToolSchema.Of(MixId),
        },
        Server("save_mix_as", ToolRiskDto.Change, Core001Handlers.SaveMixAsAsync) with
        {
            Title = "Save the mix as",
            Description = "Save the open mixer's current settings as a new mix with a name.",
            Schema = ToolSchema.Of(TrackId, Param.Text("name", 1, 100), FromOpenMixer),
            Group = ToolGroup.Mixer,
            Availability = ToolAvailability.Mixer,
            UndoKind = UndoKindDto.Compensate,
        },
        Server("update_mix", ToolRiskDto.Change, Core001Handlers.UpdateMixAsync) with
        {
            Title = "Update the mix",
            Description = "Overwrite a saved mix with the open mixer's current settings (the name stays). When the user asks to save the changes in this mix, call it at once with the page's open mix id (no question first); save_mix_as makes a new mix.",
            Schema = ToolSchema.Of(MixId, FromOpenMixer),
            Group = ToolGroup.Mixer,
            Availability = ToolAvailability.Mixer,
            UndoKind = UndoKindDto.Inverse,
        },
        Server("duplicate_mix", ToolRiskDto.Change, Core001Handlers.DuplicateMixAsync) with
        {
            Title = "Duplicate a mix", Description = "Copy a saved mix as '<name> (copy)'; use rename_mix afterwards when the user named the copy.", Schema = ToolSchema.Of(MixId), Group = ToolGroup.Tracks, UndoKind = UndoKindDto.Compensate,
        },
        Server("rename_mix", ToolRiskDto.Change, Core001Handlers.RenameMixAsync) with
        {
            Title = "Rename a mix", Description = "Rename a saved mix (the settings stay).", Schema = ToolSchema.Of(MixId, Param.Text("name", 1, 100)), Group = ToolGroup.Tracks, UndoKind = UndoKindDto.Inverse,
        },
        Server("delete_mix", ToolRiskDto.Destructive, Core001Handlers.DeleteMixAsync) with
        {
            Title = "Delete a mix", Description = "Delete a saved mix. Cannot be undone; the user must confirm.", Schema = ToolSchema.Of(MixId), Group = ToolGroup.Tracks, TargetArgument = "mixId",
        },
    ];

    private static IEnumerable<AgentToolDescriptor> Mixer() =>
    [
        Ui("mixer_get_state", ToolRiskDto.Read) with
        {
            Title = "Read the mixer",
            Description = "Read the open mixer: channel levels in dB, pan, mute, solo, master, position, playing, loop, metronome, practice tempo (read-only), unsaved changes.",
            Group = ToolGroup.Mixer,
            Availability = ToolAvailability.Mixer,
            MapUiResult = MixProjections.State,
        },
        MixerUi("mixer_set_channel") with
        {
            Title = "Set a channel",
            Description = "Change one channel of the open mixer. Use gainChangeDb for relative changes ('the vocals 3 dB louder' = gainChangeDb 3) and gainDb for an absolute level. Mute/solo/pan are optional.",
            Schema = ToolSchema.Of(
                new ToolParameter { Name = "stem", Type = ParameterType.Text, Pattern = ContractLimits.ChannelIdPattern, Description = "A stem (vocals, drums, bass, guitar, piano, other) or a detail stem as parent.detail, e.g. drums.kick." },
                GainDb, GainChangeDb, Param.Number("pan", -1, 1).Optional(), Param.Flag("mute").Optional(), Param.Flag("solo").Optional()).AtMostOne("gainDb", "gainChangeDb"),
        },
        MixerUi("mixer_set_master") with
        {
            Title = "Set the master", Description = "Change the master fader of the open mixer (absolute gainDb or relative gainChangeDb).", Schema = ToolSchema.Of(GainDb, GainChangeDb).AtMostOne("gainDb", "gainChangeDb"),
        },
        MixerUi("mixer_reset") with { Title = "Reset the mixer", Description = "Reset all channels of the open mixer to 0 dB, centre, unmuted and unsoloed." },
        MixerUi("transport") with
        {
            Title = "Play or stop",
            Description = "Play, pause, stop or seek the open mixer. Seek only moves the position: to play from a position call seek, then play.",
            Schema = ToolSchema.Of(Param.Choice("action", "play", "pause", "stop", "seek"), Param.Number("positionSeconds", 0, 7200).Optional().Describe("Position for seek.")),
            UndoKind = UndoKindDto.None,
        },
        MixerUi("set_loop") with
        {
            Title = "Set the loop",
            Description = "Set or clear the A-B loop of the open mixer in seconds or in bars of the beat grid.",
            Schema = ToolSchema.Of(Param.Number("startSeconds", 0, 7200).Optional(), Param.Number("endSeconds", 0, 7200).Optional(), Param.Whole("startBar", 1, 2000).Optional(), Param.Whole("endBar", 1, 2000).Optional(), Param.Flag("enabled")),
        },
        MixerUi("set_metronome") with
        {
            Title = "Set the metronome",
            Description = "Change the metronome of the open mixer.",
            Schema = ToolSchema.Of(Param.Flag("enabled").Optional(), Param.Number("volume", 0, 1).Optional(), Param.Whole("beatsPerBar", 2, 12).Optional(), Param.Whole("offsetMs", -2000, 2000).Optional()),
        },
    ];
}
