using StemStudio.Api.Assets;
using StemStudio.Contracts;
using StemStudio.Contracts.Scores;
using StemStudio.Domain;
using StemStudio.Domain.Assets;
using StemStudio.Domain.Scores;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Scores;

/// <summary>Creates score items and retries failed ones (FR-001, FR-003, FR-010): plan, disk guard, item, transcription job.</summary>
/// <param name="planner">Validates and plans requests.</param>
/// <param name="submitter">Starts the transcription.</param>
/// <param name="disk">Free-space guard.</param>
/// <param name="assets">Asset repository.</param>
/// <param name="reader">Maps items.</param>
public sealed class ScoreService(ScorePlanner planner, ScoreSubmitter submitter, DiskSpaceGuard disk, IAssetRepository assets, AssetService reader)
{
    /// <summary>Creates a queued score item and submits its transcription.</summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>202, 404, 409, 422 or 503.</returns>
    public async Task<IResult> CreateAsync(CreateScoreRequest request, CancellationToken cancellationToken)
    {
        var (draft, problem) = await planner.PlanAsync(request, cancellationToken);
        if (problem is not null)
        {
            return problem;
        }

        if (!disk.HasRoom())
        {
            return AssetProblems.InsufficientDiskSpace();
        }

        var name = request.Name ?? AssetNaming.DefaultName(draft!.Source.TrackTitle, ScoreRecipeRules.NameSuffix(Label(draft)), await assets.NamesAsync(cancellationToken));
        return New(name, draft!, out var asset) ?? await SubmitAsync(asset!, draft!, true, cancellationToken);
    }

    /// <summary>Plans the stored recipe of a failed score item again and resubmits it.</summary>
    /// <param name="asset">A failed score item.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>202, 404, 409, 422 or 503.</returns>
    public async Task<IResult> RetryAsync(Asset asset, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(asset);
        var (draft, problem) = await planner.ReplanAsync(asset.Provenance.ScoreRecipe!, cancellationToken);
        if (problem is not null)
        {
            return problem;
        }

        asset.Retry(submitter.Now);
        return await SubmitAsync(asset, draft!, false, cancellationToken);
    }

    /// <summary>The provenance of a draft.</summary>
    /// <param name="draft">The draft.</param>
    /// <returns>The provenance.</returns>
    public static Provenance Provenance(ScoreDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var source = draft.Source;
        return new Provenance
        {
            Origin = AssetOrigin.Transcription,
            TrackId = source.Track?.Id ?? source.Source.TrackId,
            TrackTitle = source.TrackTitle.Length <= ExportService.MaxTrackTitleLength ? source.TrackTitle : source.TrackTitle[..ExportService.MaxTrackTitleLength],
            TrackArtist = source.TrackArtist,
            SourceAssetIds = source.Source.AssetId is { } assetId ? [assetId] : [],
            Contents = ScoreRecipeRules.Contents(Label(draft)),
            ScoreRecipe = draft.Recipe,
        };
    }

    /// <summary>The channel of a track source, else the profile.</summary>
    private static string Label(ScoreDraft draft) => draft.Source.Channel ?? draft.Recipe.Profile.ToString().ToLowerInvariant();

    private IResult? New(string name, ScoreDraft draft, out Asset? asset)
    {
        try
        {
            asset = Asset.CreateScore(Guid.NewGuid(), name, Provenance(draft), submitter.Now);
            return null;
        }
        catch (DomainValidationException invalid)
        {
            asset = null;
            return AssetProblems.Invalid(invalid.Errors);
        }
    }

    private async Task<IResult> SubmitAsync(Asset asset, ScoreDraft draft, bool added, CancellationToken cancellationToken)
    {
        if (await submitter.SubmitAsync(asset, draft, cancellationToken) is { } failed)
        {
            return failed;
        }

        if (added)
        {
            assets.Add(asset);
        }

        await assets.SaveAsync(cancellationToken);
        return TypedResults.Accepted(ApiRoutes.Asset(asset.Id), await reader.ToDtoAsync(asset, cancellationToken));
    }
}
