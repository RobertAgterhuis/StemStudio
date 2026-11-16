using System.Text.Json.Nodes;
using StemStudio.Agent.Model;
using StemStudio.Agent.Ports;
using StemStudio.Contracts;
using StemStudio.Contracts.Agent;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Scores;
using static StemStudio.Agent.Tools.Providers.ToolDefinitions;

namespace StemStudio.Agent.Tools.Providers;

/// <summary>The sheet music tools of feature 008 (tool catalogue 1.0.0).</summary>
public sealed class ScoreToolProvider : IAgentToolProvider
{
    private const string F = "008";
    private const int MaxScores = 20;
    private const double DefaultMinutes = 2;

    private static readonly ToolSchema TranscribeSchema = ToolSchema.Of(
        Param.Uuid("trackId"),
        new ToolParameter { Name = "channel", Type = ParameterType.Text, Pattern = ContractLimits.ChannelIdPattern, Description = "A stem, e.g. bass, or parent.detail." },
        Param.Choice("profile", "vocals", "bass", "guitar", "keys", "piano", "drums").Optional().Describe("Default: the suggested profile of the channel."),
        Param.Choice("quality", "accurate", "fast").Optional().Describe("Piano only."));

    /// <inheritdoc />
    public IEnumerable<AgentToolDescriptor> GetTools() =>
    [
        TranscribeStemTool(),
        ListScoresTool(),
        DeleteScoreTool(),
    ];

    private static AgentToolDescriptor TranscribeStemTool() =>
        Tool(F, "transcribe_stem", ToolKindDto.Server, ToolRiskDto.Heavy) with
        {
            Title = "Transcribe a stem to sheet music",
            Description = "Make sheet music (PDF, MusicXML, MIDI) of one channel of a track, e.g. bass or vocals. Long job: the user must confirm.",
            Schema = TranscribeSchema,
            Group = ToolGroup.Tracks,
            UndoKind = UndoKindDto.Compensate,
            TargetArgument = "trackId",
            Handler = TranscribeAsync,
            Estimator = EstimateAsync,
        };

    private static AgentToolDescriptor ListScoresTool() =>
        Tool(F, "list_scores", ToolKindDto.Server, ToolRiskDto.Read) with
        {
            Title = "List sheet music",
            Description = "List the transcribed scores in the library (newest first, at most 20), optionally of one track.",
            Schema = ToolSchema.Of(Param.Uuid("trackId").Optional()),
            Group = ToolGroup.Library,
            Handler = ListAsync,
        };

    private static AgentToolDescriptor DeleteScoreTool() =>
        Tool(F, "delete_score", ToolKindDto.Server, ToolRiskDto.Destructive) with
        {
            Title = "Delete sheet music",
            Description = "Delete a score from the library. Cannot be undone: the user must confirm.",
            Schema = ToolSchema.Of(Param.Uuid("assetId").Describe("Score id from list_scores.")),
            Group = ToolGroup.Library,
            TargetArgument = "assetId",
            Handler = ExportToolProvider.DeleteAsync,
        };

    private static async Task<ToolEstimate> EstimateAsync(ToolInvocation call, CancellationToken cancellationToken)
    {
        var (options, profile, quality) = await ChoiceAsync(call, cancellationToken);
        var seconds = options.Estimates.FirstOrDefault(item => item.Profile == profile && (item.Quality is null || item.Quality == quality))?.Seconds;
        var minutes = seconds is { } value ? Math.Round(value / 60.0, 1) : DefaultMinutes;
        return new ToolEstimate($"Sheet music of {call.Text("channel")} ({Name(profile)})", minutes, "CPU");
    }

    private static async Task<ToolResult> TranscribeAsync(ToolInvocation call, CancellationToken cancellationToken)
    {
        var (options, profile, quality) = await ChoiceAsync(call, cancellationToken);
        var estimate = await EstimateAsync(call, cancellationToken);
        var request = new CreateScoreRequest { Source = options.Source, Profile = profile, Quality = profile == ScoreProfileDto.Piano ? quality : null };
        var asset = await call.Actions.Assets.CreateScoreAsync(request, cancellationToken);
        var job = new JobRef { Phase = JobPhaseDto.Transcription, JobId = asset.Id.ToString(), TrackId = call.RequiredId("trackId") };
        return new ToolResult
        {
            Projection = new JsonObject { ["assetId"] = asset.Id.ToString(), ["status"] = Name(asset.Status), ["estimatedMinutes"] = estimate.EstimatedMinutes, ["jobRef"] = JobRef(job) },
            Job = job,
            EstimatedMinutes = estimate.EstimatedMinutes,
            Undo = Undo("delete_score", new JsonObject { ["assetId"] = asset.Id.ToString() }),
        };
    }

    private static async Task<(ScoreOptionsDto Options, ScoreProfileDto Profile, ScoreQualityDto Quality)> ChoiceAsync(ToolInvocation call, CancellationToken cancellationToken)
    {
        var options = await call.Actions.Assets.GetScoreOptionsAsync(call.RequiredId("trackId"), call.Text("channel")!, cancellationToken);
        var profile = Parse<ScoreProfileDto>(call.Text("profile")) ?? options.SuggestedProfile
            ?? throw new AgentActionException("Choose a profile for this channel.");
        return (options, profile, Parse<ScoreQualityDto>(call.Text("quality")) ?? options.DefaultQuality);
    }

    private static async Task<ToolResult> ListAsync(ToolInvocation call, CancellationToken cancellationToken)
    {
        var list = await call.Actions.Assets.ListAssetsAsync(new AssetQuery { Kind = AssetKindDto.Score, TrackId = call.Id("trackId"), Limit = MaxScores }, cancellationToken);
        return ToolResult.Of(new JsonArray([.. list.Items.Take(MaxScores).Select(asset => (JsonNode)new JsonObject
        {
            ["assetId"] = asset.Id.ToString(),
            [ResultProjector.UntrustedField] = Untrusted(asset.Name),
            ["profile"] = asset.Score is { } score ? Name(score.Profile) : null,
            ["status"] = Name(asset.Status),
            ["createdAt"] = asset.CreatedAt,
        })]));
    }

    private static T? Parse<T>(string? value)
        where T : struct, Enum => value is null ? null : System.Text.Json.JsonSerializer.Deserialize<T>($"\"{value}\"", Json);
}
