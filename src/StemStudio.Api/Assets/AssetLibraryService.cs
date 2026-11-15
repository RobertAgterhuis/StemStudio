using StemStudio.Api.Worker;
using StemStudio.Contracts.Assets;
using StemStudio.Domain;
using StemStudio.Domain.Assets;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Assets;

/// <summary>The library: list and delete (FR-007, FR-009).</summary>
/// <param name="assets">Asset repository.</param>
/// <param name="files">Asset file store.</param>
/// <param name="reader">Maps items with the availability of their track.</param>
/// <param name="worker">Worker client (cancels active renders).</param>
public sealed class AssetLibraryService(IAssetRepository assets, IAssetFileStore files, AssetService reader, IWorkerClient worker)
{
    /// <summary>One page of items, newest first.</summary>
    /// <param name="request">The query values.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200 or 422.</returns>
    public async Task<IResult> ListAsync(AssetListQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var errors = new List<DomainError>();
        var query = request.ToQuery(errors);
        if (errors.Count > 0)
        {
            return AssetProblems.Invalid(errors);
        }

        var page = await assets.ListAsync(query, cancellationToken);
        return TypedResults.Ok(new AssetListDto(await reader.ToDtosAsync(page.Items, cancellationToken), page.Total, page.TotalSizeBytes));
    }

    /// <summary>Deletes an item and its folder; an active render is cancelled first (best effort).</summary>
    /// <param name="id">Item ID.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>204 or 404.</returns>
    public async Task<IResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var asset = await assets.FindAsync(id, cancellationToken);
        if (asset is null)
        {
            return AssetProblems.NotFound(id);
        }

        await CancelRenderAsync(asset, cancellationToken);
        await assets.RemoveAsync(asset, cancellationToken);
        files.DeleteAll(id);
        return TypedResults.NoContent();
    }

    private async Task CancelRenderAsync(Asset asset, CancellationToken cancellationToken)
    {
        if (asset.WorkerJobId is not { } jobId)
        {
            return;
        }

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
