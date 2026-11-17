using StemStudio.Api.Assets;
using StemStudio.Domain.Assets;
using StemStudio.Domain.Rendering;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Recording;

/// <summary>
/// Resolves the take an export names: unknown 404, another track's take 422 <c>take-not-in-track</c>, a take that is not
/// ready 409 <c>take-not-ready</c>. A saved mix's take that no longer exists is ignored (the mix shows no take either).
/// </summary>
/// <param name="takes">Take repository.</param>
/// <param name="assets">Asset repository.</param>
public sealed class TakeExportResolver(IRecordingTakeRepository takes, IAssetRepository assets) : ITakeExportSource
{
    /// <inheritdoc/>
    public async Task<ResolvedTake> ResolveAsync(Guid? takeId, Guid trackId, bool fromSavedMix, CancellationToken cancellationToken)
    {
        var take = takeId is { } id ? await takes.FindAsync(id, cancellationToken) : null;
        if (take is null || (fromSavedMix && take.TrackId != trackId))
        {
            return takeId is null || fromSavedMix ? ResolvedTake.None : new ResolvedTake(null, RecordingProblems.TakeNotFound(takeId.Value));
        }

        if (take.TrackId != trackId)
        {
            return new ResolvedTake(null, RecordingProblems.NotInTrack());
        }

        var asset = await assets.FindAsync(take.AssetId, cancellationToken);
        return asset is { Status: AssetStatus.Ready }
            ? new ResolvedTake(new RenderTake(take.AssetId, take.Name, take.StartSample, take.LengthSamples), null)
            : new ResolvedTake(null, RecordingProblems.NotReady());
    }
}
