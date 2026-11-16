using System.Text.Json.Nodes;
using StemStudio.Agent.Model;
using StemStudio.Agent.Ports;
using StemStudio.Contracts.Agent;
using StemStudio.Contracts.Lyrics;
using static StemStudio.Agent.Tools.Providers.ToolDefinitions;

namespace StemStudio.Agent.Tools.Providers;

/// <summary>
/// The synced lyrics tools of feature 006. The agent never sees lyrics text: the backend reduces lyrics to
/// <see cref="LyricsSummary"/> before they reach the agent (FR-026). Entering or editing lyrics is not a tool.
/// </summary>
public sealed class LyricsToolProvider : IAgentToolProvider
{
    private const string F = "006";
    private const double AlignMinutesPerAudioMinute = 0.5;
    private const double DefaultAudioMinutes = 4;
    private static readonly ToolParameter TrackId = Param.Uuid("trackId").Describe("Track id from search_library.");

    /// <inheritdoc />
    public IEnumerable<AgentToolDescriptor> GetTools() =>
    [
        Tool(F, "get_lyrics_status", ToolKindDto.Server, ToolRiskDto.Read) with
        {
            Title = "Get the lyrics status",
            Description = "Whether a track has lyrics and how they are synced (word, line, plain). Never returns the lyrics text.",
            Schema = ToolSchema.Of(TrackId),
            Group = ToolGroup.Tracks,
            Handler = async (call, cancellationToken) => ToolResult.Of(Summary(await call.Actions.Lyrics.GetLyricsSummaryAsync(call.RequiredId("trackId"), cancellationToken))),
        },
        Tool(F, "fetch_lyrics", ToolKindDto.Server, ToolRiskDto.Job) with
        {
            Title = "Fetch lyrics",
            Description = "Search synced lyrics for a track online (LRCLIB). Use wait_for_job with phase lyrics for the result.",
            Schema = ToolSchema.Of(TrackId),
            Group = ToolGroup.Tracks,
            UndoKind = UndoKindDto.Compensate,
            Handler = FetchAsync,
        },
        Tool(F, "align_lyrics", ToolKindDto.Server, ToolRiskDto.Heavy) with
        {
            Title = "Align lyrics to the vocals",
            Description = "Sync the lyrics of a track word by word to its vocals. GPU job: the user must confirm.",
            Schema = ToolSchema.Of(TrackId),
            Group = ToolGroup.Tracks,
            TargetArgument = "trackId",
            Handler = AlignAsync,
            Estimator = EstimateAsync,
        },
        Tool(F, "show_karaoke", ToolKindDto.Ui, ToolRiskDto.Change) with
        {
            Title = "Show or hide karaoke",
            Description = "Show or hide the karaoke lyrics view of the open mixer.",
            Schema = ToolSchema.Of(Param.Flag("enabled")),
            Group = ToolGroup.Mixer,
            Availability = ToolAvailability.Mixer,
            UndoKind = UndoKindDto.Inverse,
        },
    ];

    /// <summary>Undo of fetch_lyrics (undo-only tool <c>remove_lyrics</c>).</summary>
    internal static async Task<ToolResult> RemoveAsync(ToolInvocation call, CancellationToken cancellationToken) =>
        ToolResult.Of(Summary(await call.Actions.Lyrics.RemoveLyricsAsync(call.RequiredId("trackId"), cancellationToken)));

    private static async Task<ToolResult> FetchAsync(ToolInvocation call, CancellationToken cancellationToken)
    {
        var trackId = call.RequiredId("trackId");
        var summary = await call.Actions.Lyrics.SearchLyricsAsync(trackId, cancellationToken);
        var job = new JobRef { TrackId = trackId, Phase = JobPhaseDto.Lyrics };
        var projection = new JsonObject { ["status"] = Name(summary.Status), ["jobRef"] = JobRef(job) };
        return new ToolResult { Projection = projection, Job = job, Undo = Undo("remove_lyrics", new JsonObject { ["trackId"] = trackId.ToString() }) };
    }

    private static async Task<ToolResult> AlignAsync(ToolInvocation call, CancellationToken cancellationToken)
    {
        var trackId = call.RequiredId("trackId");
        var estimate = await EstimateAsync(call, cancellationToken);
        var summary = await call.Actions.Lyrics.AlignLyricsAsync(trackId, cancellationToken);
        var job = new JobRef { TrackId = trackId, Phase = JobPhaseDto.Lyrics };
        var projection = new JsonObject { ["status"] = Name(summary.Status), ["estimatedMinutes"] = estimate.EstimatedMinutes, ["jobRef"] = JobRef(job) };
        return new ToolResult { Projection = projection, Job = job, EstimatedMinutes = estimate.EstimatedMinutes };
    }

    private static async Task<ToolEstimate> EstimateAsync(ToolInvocation call, CancellationToken cancellationToken)
    {
        var track = await call.Actions.Tracks.GetTrackAsync(call.RequiredId("trackId"), cancellationToken);
        var minutes = (track.DurationSeconds / 60 ?? DefaultAudioMinutes) * AlignMinutesPerAudioMinute;
        return new ToolEstimate($"Align the lyrics of a track ({track.Title})", Math.Round(minutes, 1), "GPU");
    }

    /// <summary>A fixed message per status; the backend's problem text is dropped (it may quote lyrics, FR-026).</summary>
    private static string ProblemText(LyricsStatusDto status) => status switch
    {
        LyricsStatusDto.NotFound => "No lyrics were found for this track.",
        LyricsStatusDto.Disabled => "Online lyrics search is switched off.",
        LyricsStatusDto.Instrumental => "The track is instrumental.",
        _ => "The lyrics could not be loaded.",
    };

    private static JsonObject Summary(LyricsSummary summary) => new()
    {
        ["status"] = Name(summary.Status),
        ["syncLevel"] = summary.SyncLevel is { } level ? Name(level) : null,
        ["origin"] = summary.Origin is { } origin ? Name(origin) : null,
        ["source"] = summary.Source is { } source ? Name(source) : null,
        ["instrumental"] = summary.Instrumental,
        ["reason"] = summary.Reason is null ? null : ProblemText(summary.Status),
    };
}
