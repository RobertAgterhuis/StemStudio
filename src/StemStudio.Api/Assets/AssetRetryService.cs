using StemStudio.Contracts;
using StemStudio.Domain.Assets;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Assets;

/// <summary>Renders a failed item again from its stored recipe (FR-010).</summary>
/// <param name="assets">Asset repository.</param>
/// <param name="tracks">Track repository.</param>
/// <param name="submitter">Starts the render.</param>
/// <param name="reader">Maps the item.</param>
public sealed class AssetRetryService(IAssetRepository assets, ITrackRepository tracks, AssetSubmitter submitter, AssetService reader)
{
    /// <summary>Re-plans and resubmits a failed item.</summary>
    /// <param name="id">Item ID.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>202, 404, 409 or 503.</returns>
    public async Task<IResult> RetryAsync(Guid id, CancellationToken cancellationToken)
    {
        var asset = await assets.FindAsync(id, cancellationToken);
        if (asset is null || asset.Status != AssetStatus.Failed)
        {
            return asset is null ? AssetProblems.NotFound(id) : AssetProblems.NotFailed();
        }

        var track = await tracks.FindAsync(asset.Provenance.Recipe.TrackId, cancellationToken);
        var (draft, problem) = ExportPlanner.Replan(track, asset.Provenance.Recipe);
        return problem ?? await ResubmitAsync(asset, draft!, cancellationToken);
    }

    private async Task<IResult> ResubmitAsync(Asset asset, ExportDraft draft, CancellationToken cancellationToken)
    {
        asset.Retry(submitter.Now);
        if (await submitter.SubmitAsync(asset, draft.Plan, cancellationToken) is { } failed)
        {
            return failed;
        }

        await assets.SaveAsync(cancellationToken);
        return TypedResults.Accepted(ApiRoutes.Asset(asset.Id), await reader.ToDtoAsync(asset, cancellationToken));
    }
}
