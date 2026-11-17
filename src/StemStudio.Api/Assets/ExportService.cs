using StemStudio.Api.Library;
using StemStudio.Contracts;
using StemStudio.Contracts.Assets;
using StemStudio.Domain.Assets;
using StemStudio.Domain.Rendering;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Assets;

/// <summary>Creates audio exports (FR-001..FR-006, FR-015): plan, disk guard, item, render job.</summary>
/// <param name="planner">Validates and plans requests.</param>
/// <param name="submitter">Starts the render.</param>
/// <param name="disk">Free-space guard.</param>
/// <param name="assets">Asset repository.</param>
/// <param name="filer">Files new items in the library folders (feature 022).</param>
public sealed class ExportService(ExportPlanner planner, AssetSubmitter submitter, DiskSpaceGuard disk, IAssetRepository assets, ILibraryFiler filer)
{
    /// <summary>Longest stored track title.</summary>
    public const int MaxTrackTitleLength = 300;

    /// <summary>Creates a queued item and starts its render.</summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>202, 404, 409, 422 or 503.</returns>
    public async Task<IResult> CreateAsync(CreateAudioExportRequest request, CancellationToken cancellationToken)
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

        var asset = Asset.Create(Guid.NewGuid(), request.Name ?? await DefaultNameAsync(draft!, cancellationToken), Provenance(draft!), submitter.Now);
        return await submitter.SubmitAsync(asset, draft!.Plan, cancellationToken) ?? await SaveAsync(asset, cancellationToken);
    }

    /// <summary>The provenance of a draft: track snapshot, contents text and recipe.</summary>
    /// <param name="draft">The draft.</param>
    /// <returns>The provenance.</returns>
    public static Provenance Provenance(ExportDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var title = draft.Track.Title;
        return new Provenance
        {
            Origin = AssetOrigin.Export,
            TrackId = draft.Track.Id,
            TrackTitle = title.Length <= MaxTrackTitleLength ? title : title[..MaxTrackTitleLength],
            TrackArtist = draft.Track.Artist,
            Contents = ExportContents.WithRange(draft.Plan.Contents, draft.Recipe.Range),
            SourceAssetIds = draft.Plan.SourceAssetIds,
            Recipe = draft.Recipe,
        };
    }

    private async Task<string> DefaultNameAsync(ExportDraft draft, CancellationToken cancellationToken) =>
        AssetNaming.DefaultName(draft.Track.Title, ExportContents.WithRange(draft.Plan.ChannelText, draft.Recipe.Range), await assets.NamesAsync(cancellationToken));

    private async Task<IResult> SaveAsync(Asset asset, CancellationToken cancellationToken)
    {
        await filer.FileAsync(asset, cancellationToken);
        assets.Add(asset);
        await assets.SaveAsync(cancellationToken);
        return TypedResults.Accepted(ApiRoutes.Asset(asset.Id), AssetMapper.ToDto(asset, trackAvailable: true));
    }
}
