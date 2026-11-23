using StemStudio.Api.Worker;
using StemStudio.Domain.Assets;

namespace StemStudio.Api.Assets;

/// <summary>
/// The worker lanes that own the job of a library item: a render job for audio, a transcription job for a score (feature
/// 008), a handout job in the insights lane for a handout (feature 020).
/// </summary>
/// <param name="Render">The render and transcription lanes.</param>
/// <param name="Insights">The insights lane.</param>
public sealed record AssetJobWorkers(IWorkerClient Render, IWorkerInsightsClient Insights)
{
    /// <summary>Cancels and removes the item's worker job in the lane that owns it.</summary>
    /// <param name="asset">The item.</param>
    /// <param name="jobId">Its job.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    public Task DeleteAsync(Asset asset, string jobId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(asset);
        return asset.Kind switch
        {
            AssetKind.Score => Render.DeleteTranscriptionJobAsync(jobId, cancellationToken),
            AssetKind.Handout => Insights.DeleteHandoutJobAsync(jobId, cancellationToken),
            _ => Render.DeleteRenderJobAsync(jobId, cancellationToken),
        };
    }
}
