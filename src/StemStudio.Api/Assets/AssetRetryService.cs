using StemStudio.Api.Scores;
using StemStudio.Contracts;
using StemStudio.Domain.Assets;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Assets;

/// <summary>Renders a failed item again from its stored recipe (FR-010).</summary>
/// <param name="assets">Asset repository.</param>
/// <param name="tracks">Track repository.</param>
/// <param name="submitter">Starts the render.</param>
/// <param name="reader">Maps the item.</param>
/// <param name="scores">Retries score items (feature 008).</param>
/// <param name="takes">Resolves a recipe's take again (feature 015).</param>
public sealed class AssetRetryService(IAssetRepository assets, ITrackRepository tracks, AssetSubmitter submitter, AssetService reader, ScoreService scores, ITakeExportSource takes)
{
    /// <summary>Re-plans and resubmits a failed item.</summary>
    /// <param name="id">Item ID.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>202, 404, 409 or 503.</returns>
    public async Task<IResult> RetryAsync(Guid id, CancellationToken cancellationToken)
    {
        var asset = await assets.FindAsync(id, cancellationToken);
        if (asset is null)
        {
            return AssetProblems.NotFound(id);
        }

        if ((AssetOriginPolicy.ManagedElsewhere(asset) ?? (asset.Status != AssetStatus.Failed ? AssetProblems.NotFailed() : null)) is { } refused)
        {
            return refused;
        }

        return asset.Kind == AssetKind.Score ? await scores.RetryAsync(asset, cancellationToken) : await RetryExportAsync(asset, cancellationToken);
    }

    private async Task<IResult> RetryExportAsync(Asset asset, CancellationToken cancellationToken)
    {
        var recipe = asset.Provenance.Recipe!;
        var track = await tracks.FindAsync(recipe.TrackId, cancellationToken);
        var take = await takes.ResolveAsync(recipe.TakeId, recipe.TrackId, fromSavedMix: false, cancellationToken);
        if (take.Problem is not null)
        {
            return AssetProblems.SourceUnavailable("The take of this item was deleted or is not ready, so it cannot be rendered again.");
        }

        var (draft, problem) = ExportPlanner.Replan(track, recipe, take.Take);
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
