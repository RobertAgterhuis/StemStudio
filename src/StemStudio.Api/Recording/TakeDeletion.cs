using StemStudio.Api.Assets;
using StemStudio.Api.Worker;
using StemStudio.Contracts.Recording;
using StemStudio.Domain;
using StemStudio.Domain.Recording;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Recording;

/// <summary>Deletes the takes of a track inside the track delete (FR-018): staged in the same unit of work, files afterwards.</summary>
public interface ITrackTakesCleanup
{
    /// <summary>Removes the takes of a track and their items from the scope (not saved) and cancels running renders.</summary>
    /// <param name="trackId">The track.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The asset ids whose folders and hashes go after the save.</returns>
    Task<IReadOnlyList<Guid>> StageAsync(Guid trackId, CancellationToken cancellationToken);

    /// <summary>Deletes the folders and audio hashes of removed take items (best effort for the files).</summary>
    /// <param name="assetIds">From <see cref="StageAsync"/>.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task DeleteFilesAsync(IReadOnlyList<Guid> assetIds, CancellationToken cancellationToken);
}

/// <summary>
/// Deletes takes (data-model 2.1 "Delete"): the asset row and <c>assets/&lt;assetId&gt;/</c>, a running render cancelled
/// (best effort), mixes that show the take lose their take channel, and the take's audio hashes are removed.
/// </summary>
/// <param name="data">Repositories.</param>
/// <param name="files">Asset folders.</param>
/// <param name="worker">Worker client.</param>
/// <param name="hashes">The audio-hash index.</param>
public sealed class TakeDeletion(TakeData data, IAssetFileStore files, IWorkerClient worker, IAudioHashRepository hashes) : ITrackTakesCleanup
{
    /// <summary>Deletes one take.</summary>
    /// <param name="takeId">The take.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>204 or 404.</returns>
    public async Task<IResult> DeleteAsync(Guid takeId, CancellationToken cancellationToken)
    {
        var take = await data.Takes.FindAsync(takeId, cancellationToken);
        if (take is null)
        {
            return RecordingProblems.TakeNotFound(takeId);
        }

        await DeleteAsync(take.TrackId, [take], cancellationToken);
        return TypedResults.NoContent();
    }

    /// <summary>Deletes several takes of a track; unknown ids and ids of other tracks are ignored.</summary>
    /// <param name="trackId">The track.</param>
    /// <param name="request">1..200 distinct ids.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200, 404 or 422.</returns>
    public async Task<IResult> DeleteManyAsync(Guid trackId, DeleteTakesRequest? request, CancellationToken cancellationToken)
    {
        if (await data.Tracks.FindAsync(trackId, cancellationToken) is null)
        {
            return Tracks.TrackService.NotFound(trackId);
        }

        var ids = request?.TakeIds ?? [];
        if (ids.Count is < 1 or > RecordingLimits.MaxTakesPerTrack || ids.Distinct().Count() != ids.Count)
        {
            return AssetProblems.Invalid([new DomainError("takeIds", $"Send 1 to {RecordingLimits.MaxTakesPerTrack} different take ids.")]);
        }

        var takes = (await data.Takes.ListByTrackAsync(trackId, cancellationToken)).Where(take => ids.Contains(take.Id)).ToList();
        await DeleteAsync(trackId, takes, cancellationToken);
        return TypedResults.Ok(new DeleteTakesResultDto { Deleted = takes.Count });
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Guid>> StageAsync(Guid trackId, CancellationToken cancellationToken) =>
        await RemoveAsync(await data.Takes.ListByTrackAsync(trackId, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public async Task DeleteFilesAsync(IReadOnlyList<Guid> assetIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(assetIds);
        foreach (var assetId in assetIds)
        {
            await hashes.RemoveOwnerAsync(assetId, cancellationToken);
            try
            {
                files.DeleteAll(assetId);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // The row is gone; a folder that is in use now is reported by the 013 storage overview as unreferenced.
            }
        }
    }

    private async Task DeleteAsync(Guid trackId, List<RecordingTake> takes, CancellationToken cancellationToken)
    {
        if (takes.Count == 0)
        {
            return;
        }

        foreach (var mix in await data.Takes.MixesShowingAsync(trackId, [.. takes.Select(take => take.Id)], cancellationToken))
        {
            mix.RemoveTake(mix.Take!.TakeId);
        }

        var assetIds = await RemoveAsync(takes, cancellationToken);
        await data.Takes.SaveAsync(cancellationToken);
        await DeleteFilesAsync(assetIds, cancellationToken);
    }

    private async Task<IReadOnlyList<Guid>> RemoveAsync(IReadOnlyList<RecordingTake> takes, CancellationToken cancellationToken)
    {
        var assets = await data.Assets.ListByIdsAsync([.. takes.Select(take => take.AssetId)], cancellationToken);
        foreach (var asset in assets.Where(asset => asset.WorkerJobId is not null))
        {
            await CancelAsync(asset.WorkerJobId!, cancellationToken);
        }

        foreach (var take in takes)
        {
            data.Takes.Remove(take);
        }

        data.Assets.RemoveRange(assets);
        return [.. takes.Select(take => take.AssetId)];
    }

    private async Task CancelAsync(string jobId, CancellationToken cancellationToken)
    {
        try
        {
            await worker.DeleteRenderJobAsync(jobId, cancellationToken);
        }
        catch (Exception exception) when (exception is WorkerUnavailableException or WorkerRequestException)
        {
            // Best effort: a late completion finds no item, and the worker sweeps the job after 24 hours.
        }
    }
}
