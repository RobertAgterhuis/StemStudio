using StemStudio.Api.Worker;
using StemStudio.Domain.Assets;

namespace StemStudio.Api.Assets;

/// <summary>The worker job of a library item: a render job for audio, a transcription job for a score (feature 008).</summary>
internal static class AssetJobs
{
    /// <summary>Cancels and removes the item's worker job.</summary>
    /// <param name="worker">Worker client.</param>
    /// <param name="asset">The item.</param>
    /// <param name="jobId">Its job.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    public static Task DeleteAsync(IWorkerClient worker, Asset asset, string jobId, CancellationToken cancellationToken) => asset.Kind == AssetKind.Score
        ? worker.DeleteTranscriptionJobAsync(jobId, cancellationToken)
        : worker.DeleteRenderJobAsync(jobId, cancellationToken);
}
