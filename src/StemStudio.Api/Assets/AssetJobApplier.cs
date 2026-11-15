using StemStudio.Api.Worker;
using StemStudio.Domain.Assets;

namespace StemStudio.Api.Assets;

/// <summary>
/// Applies a worker render job to its item (data-model 4.5): progress, failure, or completion. A completed file is copied
/// to <c>assets/&lt;id&gt;/audio.&lt;ext&gt;</c>, its SHA-256 compared with the worker's, and only then is the item ready.
/// </summary>
/// <param name="worker">Worker client.</param>
/// <param name="files">Asset file store.</param>
/// <param name="clock">Clock.</param>
public sealed class AssetJobApplier(IWorkerClient worker, IAssetFileStore files, TimeProvider clock)
{
    /// <summary>Error when the worker no longer knows the job (e.g. after a restart).</summary>
    public const string LostJob = "The processing service lost this job; try again";

    /// <summary>Error when the copied file does not match the worker's hash.</summary>
    public const string DamagedFile = "The rendered file was damaged while it was copied; try again.";

    /// <summary>Role of the audio file.</summary>
    public const string AudioRole = "audio";

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    /// <summary>Applies the job state (not saved).</summary>
    /// <param name="asset">An active item.</param>
    /// <param name="job">The job, or null when the worker answered 404.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    /// <exception cref="WorkerUnavailableException">The worker became unreachable while the file was copied.</exception>
    public Task ApplyAsync(Asset asset, WorkerRenderJob? job, CancellationToken cancellationToken)
    {
        var state = job is null ? null : new AssetJobState(job.Status, job.Progress, job.Error);
        return AssetJobState.ApplyAsync(asset, state, () => CompleteAsync(asset, job!, cancellationToken), Now);
    }

    private Task CompleteAsync(Asset asset, WorkerRenderJob job, CancellationToken cancellationToken) => AssetJobState.StoreAsync(
        () => StoreAsync(asset, job.RenderJobId, job.Result ?? throw new InvalidDataException("The completed job has no result."), cancellationToken),
        FailureReason,
        AssetJobState.Discard(asset, files, Now));

    private async Task StoreAsync(Asset asset, string renderJobId, WorkerRenderResult result, CancellationToken cancellationToken)
    {
        var fileName = $"{AudioRole}{Path.GetExtension(result.FileName).ToLowerInvariant()}";
        var stored = await files.SaveAsync(asset.Id, fileName, (stream, ct) => worker.DownloadRenderFileAsync(renderJobId, stream, ct), cancellationToken);
        if (stored.Sha256 != result.Sha256)
        {
            throw new InvalidDataException(DamagedFile);
        }

        var format = asset.Provenance.Recipe!.Format;
        var audio = new AudioFacts(format.Format, format.BitDepth, format.Mp3BitrateKbps, result.DurationSeconds, result.PeakDbfs, result.AppliedGainDb);
        asset.Complete([new AssetFile(AudioRole, fileName, result.ContentType, stored.SizeBytes, stored.Sha256)], audio, Now);
    }

    /// <summary>A readable reason, or null for an unreachable worker (kept for the next poll) and unexpected errors.</summary>
    internal static string? FailureReason(Exception exception) => exception switch
    {
        InvalidDataException invalid => invalid.Message,
        WorkerRequestException rejection => $"Copying the rendered file failed: {rejection.Detail}",
        ArgumentException => "The processing service sent an unexpected file type.",
        IOException or UnauthorizedAccessException => "Storing the rendered file failed: the disk could not be written.",
        _ => null,
    };
}
