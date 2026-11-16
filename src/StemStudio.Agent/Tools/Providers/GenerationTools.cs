using System.Text.Json;
using System.Text.Json.Nodes;
using StemStudio.Agent.Model;
using StemStudio.Contracts.Agent;
using StemStudio.Contracts.Generation;
using StemStudio.Contracts.Tracks;
using static StemStudio.Agent.Tools.Providers.ToolDefinitions;

namespace StemStudio.Agent.Tools.Providers;

/// <summary>Shared pieces of the 009/010 generation tools: projections, estimates, the discard compensation.</summary>
internal static class GenerationTools
{
    /// <summary>The undo-only compensation of a started generation.</summary>
    public const string DiscardTool = "discard_generation";

    private const double MinutesPerAudioMinute = 1.5;
    private const double DefaultSeconds = 120;

    /// <summary>Wire name of an enum value.</summary>
    public static T? Parse<T>(string? value)
        where T : struct, Enum => value is null ? null : JsonSerializer.Deserialize<T>($"\"{value}\"", Json);

    /// <summary>The estimate of a generation (GPU-only, constitution 1.3.0).</summary>
    public static ToolEstimate Estimate(string effect, double? seconds, int takes) =>
        new(effect, Math.Round((seconds ?? DefaultSeconds) / 60 * MinutesPerAudioMinute * takes, 1), "GPU");

    /// <summary>The result of a started generation: projection, job and the discard compensation.</summary>
    public static ToolResult Started(GenerationDto generation, JobPhaseDto phase, double? minutes, Guid? trackId = null)
    {
        var job = new JobRef { Phase = phase, JobId = generation.Id.ToString(), TrackId = trackId };
        return new ToolResult
        {
            Projection = new JsonObject
            {
                ["generationId"] = generation.Id.ToString(),
                ["status"] = Name(generation.Status),
                ["estimatedMinutes"] = minutes,
                ["device"] = Device(generation.Device),
                ["jobRef"] = JobRef(job),
            },
            Job = job,
            EstimatedMinutes = minutes,
            Undo = Undo(DiscardTool, new JsonObject { ["generationId"] = generation.Id.ToString() }),
        };
    }

    /// <summary>One row of list_generations (titles and style names are untrusted; no audio URLs).</summary>
    public static JsonObject Row(GenerationDto generation) => new()
    {
        ["generationId"] = generation.Id.ToString(),
        ["kind"] = Name(generation.Kind),
        ["status"] = Name(generation.Status),
        ["progress"] = generation.Progress,
        [ResultProjector.UntrustedField] = Untrusted(generation.Restyle is { } restyle ? $"{restyle.SourceTrackTitle} - {restyle.Style.Name}" : generation.Song?.Lyrics.Title),
        ["takes"] = new JsonArray([.. generation.Takes.Select(take => (JsonNode)new JsonObject
        {
            ["index"] = take.Index,
            ["durationSeconds"] = take.DurationSeconds,
            ["vocal"] = take.Vocal is { } vocal ? Name(vocal) : null,
        })]),
        ["createdAt"] = generation.CreatedAt,
    };

    /// <summary>Undo of a started generation: cancel while active, then delete.</summary>
    public static async Task<ToolResult> DiscardAsync(ToolInvocation call, CancellationToken cancellationToken)
    {
        var id = call.RequiredId("generationId");
        var generation = await call.Actions.Generations.GetGenerationAsync(id, cancellationToken);
        if (generation.Status is not (GenerationStateDto.Completed or GenerationStateDto.Failed or GenerationStateDto.Cancelled))
        {
            await call.Actions.Generations.CancelGenerationAsync(id, cancellationToken);
        }

        await call.Actions.Generations.DeleteGenerationAsync(id, cancellationToken);
        return ToolResult.Of(new JsonObject { ["deleted"] = true });
    }

    /// <summary>The device as trusted text: "GPU (name)" with a safe hardware name, or "CPU".</summary>
    public static string? Device(JobDeviceDto? device) => device is null
        ? null
        : device.Kind == ProcessingDeviceKindDto.Gpu ? $"GPU ({TrustedText.SafeName(device.Name) ?? "unknown"})" : "CPU";
}
