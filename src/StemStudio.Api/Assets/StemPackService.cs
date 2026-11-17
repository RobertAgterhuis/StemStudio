using StemStudio.Api.Library;
using StemStudio.Contracts.Assets;
using StemStudio.Domain.Assets;
using StemStudio.Domain.Rendering;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Assets;

/// <summary>
/// Saves every stem of a track, optionally with the kept detail stems of every split parent, as raw items in catalogue
/// order (FR-013). The disk guard applies once for the whole pack; each item renders as its own job.
/// </summary>
/// <param name="tracks">Track repository.</param>
/// <param name="submitter">Starts the renders.</param>
/// <param name="disk">Free-space guard.</param>
/// <param name="assets">Asset repository.</param>
/// <param name="filer">Files new items in the library folders (feature 022).</param>
public sealed class StemPackService(ITrackRepository tracks, AssetSubmitter submitter, DiskSpaceGuard disk, IAssetRepository assets, ILibraryFiler filer)
{
    /// <summary>Creates the pack.</summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>202, 404, 409, 422 or 503.</returns>
    public async Task<IResult> CreateAsync(CreateStemPackRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (ExportRequestRules.FormatErrors(request.Format).ToList() is { Count: > 0 } invalid)
        {
            return AssetProblems.Invalid(invalid);
        }

        var track = await tracks.FindAsync(request.TrackId, cancellationToken);
        var problem = ExportPlanner.TrackProblem(track, request.TrackId) ?? (disk.HasRoom() ? null : AssetProblems.InsufficientDiskSpace());
        return problem ?? await CreateItemsAsync(track!, request, cancellationToken);
    }

    private async Task<IResult> CreateItemsAsync(Track track, CreateStemPackRequest request, CancellationToken cancellationToken)
    {
        var names = (await assets.NamesAsync(cancellationToken)).ToList();
        var items = new List<Asset>();
        foreach (var channel in RenderSource.Of(track).AllChannels().Where(channel => request.IncludeDetail || channel.Detail is null))
        {
            var (asset, failed) = await SubmitAsync(ExportPlanner.RawDraft(track, channel.Id, request.Format), names, cancellationToken);
            if (failed is not null)
            {
                return failed;
            }

            items.Add(asset);
        }

        return await SaveAsync(items, cancellationToken);
    }

    private async Task<(Asset Asset, IResult? Problem)> SubmitAsync(ExportDraft draft, List<string> names, CancellationToken cancellationToken)
    {
        var name = AssetNaming.DefaultName(draft.Track.Title, draft.Plan.ChannelText, names);
        names.Add(name);
        var asset = Asset.Create(Guid.NewGuid(), name, ExportService.Provenance(draft), submitter.Now);
        return (asset, await submitter.SubmitAsync(asset, draft.Plan, cancellationToken));
    }

    private async Task<IResult> SaveAsync(List<Asset> items, CancellationToken cancellationToken)
    {
        foreach (var item in items)
        {
            await filer.FileAsync(item, cancellationToken);
            assets.Add(item);
        }

        await assets.SaveAsync(cancellationToken);
        return TypedResults.Accepted((string?)null, new StemPackDto(items.Select(asset => AssetMapper.ToDto(asset, trackAvailable: true)).ToList()));
    }
}
