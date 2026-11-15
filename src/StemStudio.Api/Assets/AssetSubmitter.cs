using StemStudio.Api.Worker;
using StemStudio.Domain.Assets;
using StemStudio.Domain.Rendering;

namespace StemStudio.Api.Assets;

/// <summary>Submits the render of an item to the worker (<c>POST /v1/render-jobs</c>) and records the job.</summary>
/// <param name="worker">Worker client.</param>
/// <param name="clock">Clock.</param>
public sealed class AssetSubmitter(IWorkerClient worker, TimeProvider clock)
{
    /// <summary>Longest file tag.</summary>
    public const int MaxTagLength = 200;

    /// <summary>Current time (UTC).</summary>
    public DateTime Now => clock.GetUtcNow().UtcDateTime;

    /// <summary>Starts the render; null on success, else the problem (503 unreachable, 409 <c>source-unavailable</c> for missing files).</summary>
    /// <param name="asset">A queued item.</param>
    /// <param name="plan">The render plan.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Null or the problem.</returns>
    public async Task<IResult?> SubmitAsync(Asset asset, RenderPlan plan, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(asset);
        try
        {
            asset.AssignJob((await worker.CreateRenderJobAsync(Request(asset.Provenance, plan), cancellationToken)).RenderJobId, Now);
            return null;
        }
        catch (Exception exception) when (SubmitProblem(exception) is { } problem)
        {
            return problem;
        }
    }

    private static IResult? SubmitProblem(Exception exception) => exception switch
    {
        WorkerUnavailableException => ApiProblems.Unavailable(),
        WorkerRequestException rejection => AssetProblems.SourceUnavailable(rejection.Detail),
        _ => null,
    };

    /// <summary>The worker request of a plan: raw single-channel renders are not dithered (bit-identical path).</summary>
    /// <param name="provenance">The item's provenance (recipe and tags).</param>
    /// <param name="plan">The plan.</param>
    /// <returns>The request.</returns>
    public static WorkerRenderJobRequest Request(Provenance provenance, RenderPlan plan)
    {
        ArgumentNullException.ThrowIfNull(provenance);
        ArgumentNullException.ThrowIfNull(plan);
        var recipe = provenance.Recipe!;
        return new WorkerRenderJobRequest
        {
            Inputs = plan.Inputs.Select(input => new WorkerRenderInput(input.Path, input.Gain, input.Pan)).ToList(),
            MasterGain = plan.MasterGain,
            Range = recipe.Range is { } range ? new WorkerRenderRange(range.StartSeconds, range.EndSeconds) : null,
            Output = Output(recipe.Format, !(plan.Mode == ExportMode.Raw && plan.Inputs.Count == 1)),
            Metadata = new WorkerRenderMetadata(Tag(provenance.TrackTitle), Tag(provenance.TrackArtist), Tag(provenance.Contents)),
        };
    }

    private static WorkerRenderOutput Output(ExportFormat format, bool dither) =>
        new(Enum.Parse<WorkerRenderFormat>(format.Format.ToString())) { BitDepth = format.BitDepth, Mp3BitrateKbps = format.Mp3BitrateKbps, Dither = dither };

    private static string? Tag(string? value) => value is null || value.Length <= MaxTagLength ? value : value[..MaxTagLength];
}
