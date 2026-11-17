using StemStudio.Api.Assets;
using StemStudio.Contracts;
using StemStudio.Contracts.Recording;
using StemStudio.Domain;
using StemStudio.Domain.Assets;
using StemStudio.Domain.Recording;

namespace StemStudio.Api.Recording;

/// <summary>Reads, edits and retries takes (<c>listRecordingTakes</c>, <c>getRecordingTake</c>, <c>updateRecordingTake</c>, <c>retryRecordingTake</c>).</summary>
/// <param name="data">Repositories.</param>
/// <param name="storage">Take files.</param>
/// <param name="renderer">Starts renders.</param>
public sealed class TakeService(TakeData data, TakeFiles storage, TakeRenderer renderer)
{
    /// <summary>The takes of a track, newest first, with their total size.</summary>
    /// <param name="trackId">The track.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200 or 404.</returns>
    public async Task<IResult> ListAsync(Guid trackId, CancellationToken cancellationToken)
    {
        if (await data.Tracks.FindAsync(trackId, cancellationToken) is null)
        {
            return Tracks.TrackService.NotFound(trackId);
        }

        var takes = await data.Takes.ListByTrackAsync(trackId, cancellationToken);
        var assets = (await data.Assets.ListByIdsAsync([.. takes.Select(take => take.AssetId)], cancellationToken)).ToDictionary(asset => asset.Id);
        var items = takes.Select(take => TakeMapper.ToDto(take, assets.GetValueOrDefault(take.AssetId))).ToList();
        return TypedResults.Ok(new RecordingTakeListDto { Items = items, TotalSizeBytes = items.Sum(item => item.SizeBytes) });
    }

    /// <summary>One take.</summary>
    /// <param name="takeId">The take.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200 or 404.</returns>
    public async Task<IResult> GetAsync(Guid takeId, CancellationToken cancellationToken)
    {
        var take = await data.Takes.FindAsync(takeId, cancellationToken);
        return take is null ? RecordingProblems.TakeNotFound(takeId) : TypedResults.Ok(await ToDtoAsync(take, cancellationToken));
    }

    /// <summary>Renames (the asset name follows), stars or changes the part of a take.</summary>
    /// <param name="takeId">The take.</param>
    /// <param name="request">At least one field.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200, 404 or 422.</returns>
    public async Task<IResult> UpdateAsync(Guid takeId, UpdateTakeRequest? request, CancellationToken cancellationToken)
    {
        var take = await data.Takes.FindAsync(takeId, cancellationToken);
        if (take is null)
        {
            return RecordingProblems.TakeNotFound(takeId);
        }

        if (request is null || (request.Name is null && request.Favorite is null && request.Part is null) || request.Part is { } part && !Enum.IsDefined(part))
        {
            return AssetProblems.Invalid([new DomainError(string.Empty, "Send a name, favorite or part.")]);
        }

        var asset = await data.Assets.FindAsync(take.AssetId, cancellationToken);
        return Apply(take, asset, request) ?? await SaveAsync(take, asset, cancellationToken);
    }

    private IResult? Apply(RecordingTake take, Asset? asset, UpdateTakeRequest request)
    {
        try
        {
            if (request.Name is { } name)
            {
                take.Rename(name, renderer.Now);
                asset?.Rename(take.Name, renderer.Now);
            }

            take.SetFavorite(request.Favorite ?? take.Favorite, renderer.Now);
            take.SetPart(request.Part is { } part ? TakeUploadRules.ToDomain(part) : take.Part, renderer.Now);
            return null;
        }
        catch (DomainValidationException invalid)
        {
            return AssetProblems.Invalid(invalid.Errors);
        }
    }

    private async Task<IResult> SaveAsync(RecordingTake take, Asset? asset, CancellationToken cancellationToken)
    {
        await data.Takes.SaveAsync(cancellationToken);
        return TypedResults.Ok(TakeMapper.ToDto(take, asset));
    }

    /// <summary>Renders a failed take again from its stored <c>capture.wav</c>.</summary>
    /// <param name="takeId">The take.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>202, 404, 409 or 503.</returns>
    public async Task<IResult> RetryAsync(Guid takeId, CancellationToken cancellationToken)
    {
        var stored = await data.FindAsync(takeId, cancellationToken);
        if (stored is not { Asset.Status: AssetStatus.Failed })
        {
            return stored is null ? RecordingProblems.TakeNotFound(takeId) : RecordingProblems.NotFailed();
        }

        return storage.HasCapture(stored.Asset.Id) ? await RerenderAsync(stored.Take, stored.Asset, cancellationToken) : RecordingProblems.CaptureMissing();
    }

    private async Task<IResult> RerenderAsync(RecordingTake take, Asset asset, CancellationToken cancellationToken)
    {
        asset.Rerender(renderer.Now);
        if (await renderer.StartAsync(asset, cancellationToken) is { } unavailable)
        {
            return unavailable;
        }

        await data.Takes.SaveAsync(cancellationToken);
        return TypedResults.Accepted(ApiRoutes.RecordingTakeOf(take.Id), TakeMapper.ToDto(take, asset));
    }

    /// <summary>Maps a take with its asset.</summary>
    /// <param name="take">The take.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The DTO.</returns>
    public async Task<RecordingTakeDto> ToDtoAsync(RecordingTake take, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(take);
        return TakeMapper.ToDto(take, await data.Assets.FindAsync(take.AssetId, cancellationToken));
    }
}
