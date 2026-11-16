using System.Text.Json.Nodes;
using StemStudio.Agent.Model;
using StemStudio.Contracts.Agent;
using StemStudio.Contracts.Tracks;
using static StemStudio.Agent.Tools.Providers.ToolDefinitions;

namespace StemStudio.Agent.Tools.Providers;

/// <summary>The detail separation tools of feature 002 (tool catalogue 1.0.0).</summary>
public sealed class Detail002ToolProvider : IAgentToolProvider
{
    private const string F = "002";
    private const double GpuMinutesPerAudioMinute = 1.0;
    private const double CpuMinutesPerAudioMinute = 7.5;

    /// <inheritdoc />
    public IEnumerable<AgentToolDescriptor> GetTools() =>
    [
        Tool(F, "start_detail_separation", ToolKindDto.Server, ToolRiskDto.Heavy) with
        {
            Title = "Start detail separation",
            Description = "Split the stems of a completed track further (kick, snare, lead and backing vocals, instruments). Long GPU job: the user must confirm.",
            Schema = ToolSchema.Of(TrackId),
            Group = ToolGroup.Tracks,
            UndoKind = UndoKindDto.Inverse,
            TargetArgument = "trackId",
            Handler = StartAsync,
            Estimator = EstimateAsync,
        },
        Tool(F, "cancel_detail_separation", ToolKindDto.Server, ToolRiskDto.Change) with
        {
            Title = "Cancel detail separation",
            Description = "Cancel a queued or running detail separation.",
            Schema = ToolSchema.Of(TrackId),
            Group = ToolGroup.Tracks,
            Handler = CancelAsync,
        },
    ];

    private static async Task<ToolEstimate> EstimateAsync(ToolInvocation call, CancellationToken cancellationToken)
    {
        var track = await call.Actions.Tracks.GetTrackAsync(call.RequiredId("trackId"), cancellationToken);
        var device = await call.Actions.Tracks.GetDeviceAsync(cancellationToken);
        var gpu = device.Kind == ProcessingDeviceKindDto.Gpu;
        var minutes = (track.DurationSeconds / 60 ?? DefaultAudioMinutes) * (gpu ? GpuMinutesPerAudioMinute : CpuMinutesPerAudioMinute);
        return new ToolEstimate($"Detail separation of a track ({track.Title})", Math.Round(minutes, 1), gpu ? $"GPU ({device.Name})" : "CPU");
    }

    private static async Task<ToolResult> StartAsync(ToolInvocation call, CancellationToken cancellationToken)
    {
        var estimate = await EstimateAsync(call, cancellationToken);
        var track = await call.Actions.Tracks.StartDetailAsync(call.RequiredId("trackId"), cancellationToken);
        var job = new JobRef { TrackId = track.Id, Phase = JobPhaseDto.Detail };
        var undo = Undo("cancel_detail_separation", new JsonObject { ["trackId"] = track.Id.ToString() });
        return new ToolResult { Projection = Started(track, estimate, job), Job = job, EstimatedMinutes = estimate.EstimatedMinutes, Undo = undo };
    }

    private static JsonObject Started(TrackDto track, ToolEstimate estimate, JobRef job) => new()
    {
        ["trackId"] = track.Id.ToString(),
        ["detailStatus"] = Name(track.Detail.Status),
        ["estimatedMinutes"] = estimate.EstimatedMinutes,
        ["device"] = estimate.Device,
        ["jobRef"] = JobRef(job),
    };

    private static async Task<ToolResult> CancelAsync(ToolInvocation call, CancellationToken cancellationToken)
    {
        var track = await call.Actions.Tracks.CancelDetailAsync(call.RequiredId("trackId"), cancellationToken);
        return ToolResult.Of(new JsonObject { ["detailStatus"] = Name(track.Detail.Status) });
    }
}
