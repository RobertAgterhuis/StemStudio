using StemStudio.Contracts.Library;
using StemStudio.Domain;
using StemStudio.Domain.Assets;
using StemStudio.Domain.Library;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Library;

/// <summary>
/// Library info of tracks and items (feature 022, FR-031..FR-033, backend-api 1.22.0 <c>*LibraryInfo</c>): reads the
/// user names with their effective and automatic names, sets or resets them and re-files the affected items in the
/// same save. Names are untrusted text: they are validated by <see cref="LibraryInfoRules"/> and only ever become
/// display names and hashed folder ids, never file paths.
/// </summary>
/// <param name="library">Library data access.</param>
/// <param name="filer">Filer (re-files after a change).</param>
/// <param name="clock">Clock.</param>
public sealed class LibraryInfoService(ILibraryRepository library, ILibraryFiler filer, TimeProvider clock)
{
    private static readonly FolderLevel[] Levels = [FolderLevel.Artist, FolderLevel.Album, FolderLevel.Song];

    /// <summary>GET of a track (<c>getTrackLibraryInfo</c>).</summary>
    /// <param name="trackId">Track ID.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200 or 404.</returns>
    public async Task<IResult> GetTrackAsync(Guid trackId, CancellationToken cancellationToken)
    {
        var track = await library.FindTrackAsync(trackId, cancellationToken);
        return track is null ? LibraryInfoProblems.TrackNotFound(trackId) : TypedResults.Ok(TrackState(track, 0));
    }

    /// <summary>PUT of a track (<c>setTrackLibraryInfo</c>): sets the names and re-files every item of the track.</summary>
    /// <param name="trackId">Track ID.</param>
    /// <param name="request">The names (null = automatic).</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200, 400 or 404.</returns>
    public async Task<IResult> SetTrackAsync(Guid trackId, LibraryInfoRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var track = await library.FindTrackAsync(trackId, cancellationToken);
        if (track is null)
        {
            return LibraryInfoProblems.TrackNotFound(trackId);
        }

        if (Apply(() => track.SetLibraryInfo(ToInfo(request), Now)) is { } invalid)
        {
            return invalid;
        }

        var moved = await filer.RefileTrackAsync(track, cancellationToken);
        await library.SaveAsync(cancellationToken);
        return TypedResults.Ok(TrackState(track, moved));
    }

    /// <summary>DELETE of a track (<c>resetTrackLibraryInfo</c>): every level automatic again.</summary>
    /// <param name="trackId">Track ID.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200 or 404.</returns>
    public Task<IResult> ResetTrackAsync(Guid trackId, CancellationToken cancellationToken) =>
        SetTrackAsync(trackId, LibraryInfoRequest.Automatic, cancellationToken);

    /// <summary>GET of an item (<c>getAssetLibraryInfo</c>), with its placement.</summary>
    /// <param name="assetId">Item ID.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200 or 404.</returns>
    public async Task<IResult> GetItemAsync(Guid assetId, CancellationToken cancellationToken)
    {
        var asset = await library.FindAssetAsync(assetId, cancellationToken);
        return asset is null ? LibraryInfoProblems.ItemNotFound(assetId) : TypedResults.Ok(await ItemStateAsync(asset, 0, cancellationToken));
    }

    /// <summary>PUT of an item (<c>setAssetLibraryInfo</c>): sets the item's own names and re-files it.</summary>
    /// <param name="assetId">Item ID.</param>
    /// <param name="request">The names (null = the track's or the automatic name).</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200, 400, 404 or 409 <c>not-fileable</c>.</returns>
    public async Task<IResult> SetItemAsync(Guid assetId, LibraryInfoRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var asset = await library.FindAssetAsync(assetId, cancellationToken);
        if (asset is null)
        {
            return LibraryInfoProblems.ItemNotFound(assetId);
        }

        if (!LibraryFilingRules.IsFileable(asset.Provenance))
        {
            return LibraryInfoProblems.NotFileable();
        }

        return Apply(() => asset.SetLibraryInfo(ToInfo(request), Now)) ?? await RefileItemAsync(asset, cancellationToken);
    }

    /// <summary>DELETE of an item (<c>resetAssetLibraryInfo</c>): the track's or automatic names again.</summary>
    /// <param name="assetId">Item ID.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200, 404 or 409 <c>not-fileable</c>.</returns>
    public Task<IResult> ResetItemAsync(Guid assetId, CancellationToken cancellationToken) =>
        SetItemAsync(assetId, LibraryInfoRequest.Automatic, cancellationToken);

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    private async Task<IResult> RefileItemAsync(Asset asset, CancellationToken cancellationToken)
    {
        var moved = await filer.FileAsync(asset, cancellationToken) ? 1 : 0;
        await library.SaveAsync(cancellationToken);
        return TypedResults.Ok(await ItemStateAsync(asset, moved, cancellationToken));
    }

    private static IResult? Apply(Action change)
    {
        try
        {
            change();
            return null;
        }
        catch (DomainValidationException invalid)
        {
            return LibraryInfoProblems.Invalid(invalid);
        }
    }

    private static LibraryInfo ToInfo(LibraryInfoRequest request) => new() { Artist = request.Artist, Album = request.Album, Song = request.Song };

    private static LibraryInfoStateDto TrackState(Track track, int moved)
    {
        var input = new PlacementInput { Provenance = ProvenanceOf(track), TrackInfo = track.LibraryInfo, Track = TrackFacts.From(track) };
        return State(track.LibraryInfo, PlacementResolver.Resolve(input), PlacementResolver.Automatic(input)) with { MovedItems = moved };
    }

    private async Task<LibraryInfoStateDto> ItemStateAsync(Asset asset, int moved, CancellationToken cancellationToken)
    {
        var input = await filer.InputAsync(asset, cancellationToken);
        var automatic = PlacementResolver.Resolve(input with { ItemInfo = null });
        return State(asset.LibraryInfo, PlacementResolver.Resolve(input), automatic) with { Placement = LibraryMapper.PlacementOf(asset), MovedItems = moved };
    }

    private static Provenance ProvenanceOf(Track track) => new()
    {
        Origin = AssetOrigin.Export,
        TrackId = track.Id,
        TrackTitle = track.Title,
        TrackArtist = track.Artist,
        Contents = string.Empty,
    };

    private static LibraryInfoStateDto State(LibraryInfo values, Placement effective, Placement automatic)
    {
        var levels = Levels.Select(level => new LibraryInfoLevelDto
        {
            Value = values.For(level),
            Effective = effective.Of(level).Name,
            Source = LibraryMapper.ToDto(effective.Of(level).Source),
            Automatic = automatic.Of(level).Name,
        }).ToArray();
        return new LibraryInfoStateDto { Artist = levels[0], Album = levels[1], Song = levels[2], Placement = null, MovedItems = 0 };
    }
}
