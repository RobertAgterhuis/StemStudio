using System.Text.Json.Nodes;
using StemStudio.Agent.Model;
using StemStudio.Agent.Ports;
using StemStudio.Contracts;
using StemStudio.Contracts.Agent;
using StemStudio.Contracts.Assets;
using static StemStudio.Agent.Tools.Providers.ToolDefinitions;

namespace StemStudio.Agent.Tools.Providers;

/// <summary>
/// The export library tools of feature 007. Exports are jobs; a call whose output is estimated above 500 MB is escalated to
/// <c>heavy</c> (approval). Each export's undo is the fresh deletion of what it created (tool catalogue 1.0.0).
/// </summary>
public sealed class ExportToolProvider : IAgentToolProvider
{
    private const string F = "007";
    private const int MaxExports = 20;
    private static readonly ToolParameter TrackId = Param.Uuid("trackId").Describe("Track id from search_library.");
    private static readonly ToolParameter AssetId = Param.Uuid("assetId").Describe("Export id from list_exports.");
    private static readonly ToolParameter Format = Param.Choice("format", "flac", "wav", "mp3");
    private static readonly ToolParameter Channel = new() { Name = "channel", Type = ParameterType.Text, Pattern = ContractLimits.ChannelIdPattern };

    private static readonly ToolSchema AudioSchema = ToolSchema.Of(
        TrackId,
        Param.Choice("mode", "raw", "mix"),
        Param.List("channels", Channel, 1, 20).Optional(),
        Param.Uuid("mixId").Optional(),
        Format,
        Param.Whole("bitDepth", 16, 24).Optional().Describe("16 or 24 (wav/flac)."),
        Param.Whole("mp3BitrateKbps", 192, 320).Optional().Describe("192, 256 or 320 (mp3)."),
        Param.Record("range", Param.Number("startSeconds", 0, 36_000), Param.Number("endSeconds", 0, 36_000)).Optional(),
        Param.Text("name", 1, 100).Optional());

    /// <inheritdoc />
    public IEnumerable<AgentToolDescriptor> GetTools() =>
    [
        ExportAudioTool(),
        ExportStemPackTool(),
        ListExportsTool(),
        DeleteExportTool(),
    ];

    private static AgentToolDescriptor ExportAudioTool() =>
        Tool(F, "export_audio", ToolKindDto.Server, ToolRiskDto.Job) with
        {
            Title = "Export audio",
            Description = "Export a track as an audio file: mode raw (chosen channels, e.g. only vocals) or mix (what the user hears: the open mixer, or a saved mixId). Optional range in seconds and a name.",
            Schema = AudioSchema,
            Group = ToolGroup.Tracks,
            UndoKind = UndoKindDto.Compensate,
            Handler = ExportAsync,
            HeavyWhen = (call, cancellationToken) => ExportSizes.AboveLimitAsync(call, 1, cancellationToken),
            Estimator = (call, cancellationToken) => ExportSizes.EstimateAsync(call, 1, cancellationToken),
        };

    private static AgentToolDescriptor ExportStemPackTool() =>
        Tool(F, "export_stem_pack", ToolKindDto.Server, ToolRiskDto.Job) with
        {
            Title = "Export a stem pack",
            Description = "Export every stem of a track as separate files (optionally the detail stems too).",
            Schema = ToolSchema.Of(TrackId, Format, Param.Flag("includeDetail").Optional()),
            Group = ToolGroup.Library,
            UndoKind = UndoKindDto.Compensate,
            Handler = StemPackAsync,
            HeavyWhen = (call, cancellationToken) => ExportSizes.AboveLimitAsync(call, null, cancellationToken),
            Estimator = (call, cancellationToken) => ExportSizes.EstimateAsync(call, null, cancellationToken),
        };

    private static AgentToolDescriptor ListExportsTool() =>
        Tool(F, "list_exports", ToolKindDto.Server, ToolRiskDto.Read) with
        {
            Title = "List exports",
            Description = "List the audio exports in the library (newest first, at most 20), optionally of one track or matching a name.",
            Schema = ToolSchema.Of(TrackId.Optional(), Param.Text("query", 1, 100).Optional()),
            Group = ToolGroup.Library,
            Handler = ListAsync,
        };

    private static AgentToolDescriptor DeleteExportTool() =>
        Tool(F, "delete_export", ToolKindDto.Server, ToolRiskDto.Destructive) with
        {
            Title = "Delete an export",
            Description = "Delete an audio export from the library. Cannot be undone: the user must confirm.",
            Schema = ToolSchema.Of(AssetId),
            Group = ToolGroup.Library,
            TargetArgument = "assetId",
            Handler = DeleteAsync,
        };

    internal static async Task<ToolResult> DeleteAsync(ToolInvocation call, CancellationToken cancellationToken)
    {
        await call.Actions.Assets.DeleteAssetAsync(call.RequiredId("assetId"), cancellationToken);
        return ToolResult.Of(new JsonObject { ["deleted"] = true });
    }

    private static async Task<ToolResult> ExportAsync(ToolInvocation call, CancellationToken cancellationToken)
    {
        var request = await ExportRequests.AudioAsync(call, cancellationToken);
        var asset = await call.Actions.Assets.CreateAudioExportAsync(request, cancellationToken);
        var job = new JobRef { Phase = JobPhaseDto.Export, JobId = asset.Id.ToString(), TrackId = request.TrackId };
        return new ToolResult
        {
            Projection = new JsonObject { ["assetId"] = asset.Id.ToString(), ["status"] = Name(asset.Status), ["jobRef"] = JobRef(job) },
            Job = job,
            Undo = DeleteUndo(asset.Id),
            UndoFresh = true,
        };
    }

    private static async Task<ToolResult> StemPackAsync(ToolInvocation call, CancellationToken cancellationToken)
    {
        var request = new CreateStemPackRequest(call.RequiredId("trackId"), ExportRequests.Format(call), call.Flag("includeDetail") == true);
        var pack = await call.Actions.Assets.CreateStemPackAsync(request, cancellationToken);
        var status = pack.Items.Count == 0 ? null : Name(pack.Items[0].Status);
        return new ToolResult
        {
            Projection = new JsonObject { ["assetIds"] = new JsonArray([.. pack.Items.Select(item => (JsonNode)item.Id.ToString())]), ["status"] = status },
            MoreUndo = [.. pack.Items.Select(item => DeleteUndo(item.Id))],
            UndoFresh = true,
        };
    }

    private static async Task<ToolResult> ListAsync(ToolInvocation call, CancellationToken cancellationToken)
    {
        var query = new AssetQuery { Kind = AssetKindDto.Audio, TrackId = call.Id("trackId"), Query = call.Text("query"), Limit = MaxExports };
        var list = await call.Actions.Assets.ListAssetsAsync(query, cancellationToken);
        return ToolResult.Of(new JsonArray([.. list.Items.Take(MaxExports).Select(asset => (JsonNode)new JsonObject
        {
            ["assetId"] = asset.Id.ToString(),
            [ResultProjector.UntrustedField] = Untrusted(asset.Name),
            ["format"] = asset.Audio is { } audio ? Name(audio.Format) : null,
            ["status"] = Name(asset.Status),
            ["sizeBytes"] = asset.SizeBytes,
            ["createdAt"] = asset.CreatedAt,
        })]));
    }

    private static UndoAction DeleteUndo(Guid assetId) => Undo("delete_export", new JsonObject { ["assetId"] = assetId.ToString() });
}
