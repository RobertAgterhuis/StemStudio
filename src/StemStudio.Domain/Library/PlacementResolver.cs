using StemStudio.Domain.Assets;
using StemStudio.Domain.Tracks;

namespace StemStudio.Domain.Library;

/// <summary>
/// Chooses the placement of an item (feature 022, research R-01, FR-003/FR-006/FR-008), each level independently:
/// item info &gt; track info &gt; match &gt; import &gt; fallback. Without a track the source item's placement is used,
/// then the item's previous placement (track and source gone), then the provenance snapshot; an item that never had a
/// track or a source item is filed under "My songs" &gt; "Songs from my lyrics" &gt; its title. Pure.
/// </summary>
public static class PlacementResolver
{
    private static readonly FolderLevel[] Levels = [FolderLevel.Artist, FolderLevel.Album, FolderLevel.Song];

    /// <summary>Resolves the placement.</summary>
    /// <param name="input">The facts.</param>
    /// <returns>The placement under the current rules (<see cref="LibraryFilingRules.Version"/>).</returns>
    public static Placement Resolve(PlacementInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var automatic = Automatic(input);
        var levels = Levels.Select(level => Choose(level, input, automatic.Of(level))).ToArray();
        return new Placement
        {
            Artist = levels[0],
            Album = levels[1],
            Song = levels[2],
            Year = ReferenceEquals(levels[1], automatic.Album) ? automatic.Year : null,
        };
    }

    /// <summary>The names automatic filing would use (no item info, no track info).</summary>
    /// <param name="input">The facts.</param>
    /// <returns>The automatic placement.</returns>
    public static Placement Automatic(PlacementInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Track is { } track)
        {
            return FromTrack(track, input.Provenance);
        }

        return input.SourcePlacement ?? FromPrevious(input) ?? FromProvenance(input.Provenance);
    }

    private static PlacementLevel Choose(FolderLevel level, PlacementInput input, PlacementLevel automatic) =>
        Pick(level, [(input.ItemInfo?.For(level), PlacementSource.Item), (input.TrackInfo?.For(level), PlacementSource.Track)]) ?? automatic;

    private static Placement FromTrack(TrackFacts track, Provenance provenance)
    {
        var metadata = track.Metadata;
        var match = metadata.Status == MetadataStatus.Found ? metadata : TrackMetadata.None;
        var album = Pick(FolderLevel.Album, [(match.Album, PlacementSource.Match), (metadata.LookupAlbum, PlacementSource.Import)]);
        return Build(
            Pick(FolderLevel.Artist, [(MatchedArtist(match, track.ArtistMbid), PlacementSource.Match), (metadata.LookupArtist, PlacementSource.Import), (track.Artist, PlacementSource.Import), (provenance.TrackArtist, PlacementSource.Import)]),
            album,
            Pick(FolderLevel.Song, [(match.RecordingTitle, PlacementSource.Match), (track.LookupTitle, PlacementSource.Import), (track.Title, PlacementSource.Import), (provenance.TrackTitle, PlacementSource.Import)]),
            album?.Source == PlacementSource.Match ? YearOf(metadata.Year) : null);
    }

    private static string? MatchedArtist(TrackMetadata match, Guid? artistMbid) =>
        LibraryKey.IsVariousArtists(match.ArtistName, artistMbid) ? null : match.ArtistName;

    private static Placement? FromPrevious(PlacementInput input)
    {
        if (input.Previous is not { } previous || !RefersToOthers(input.Provenance))
        {
            return null;
        }

        var snapshot = FromProvenance(input.Provenance);
        var levels = Levels.Select(level => Kept(level, previous, snapshot)).ToArray();
        return new Placement { Artist = levels[0], Album = levels[1], Song = levels[2], Year = levels[1] == previous.Album ? previous.Year : null };
    }

    private static PlacementLevel Kept(FolderLevel level, Placement previous, Placement snapshot) =>
        previous.Of(level) is { Source: not PlacementSource.Item } kept ? PlacementLevel.Of(level, kept.Name, kept.Source) : snapshot.Of(level);

    private static Placement FromProvenance(Provenance provenance)
    {
        var song = Pick(FolderLevel.Song, [(provenance.TrackTitle, PlacementSource.Import)]);
        if (!RefersToOthers(provenance))
        {
            return Build(PlacementLevel.Of(FolderLevel.Artist, LibraryKey.OwnSongsArtist, PlacementSource.Fallback), PlacementLevel.Of(FolderLevel.Album, LibraryKey.OwnSongsAlbum, PlacementSource.Fallback), song, null);
        }

        return Build(Pick(FolderLevel.Artist, [(provenance.TrackArtist, PlacementSource.Import)]), null, song, null);
    }

    private static bool RefersToOthers(Provenance provenance) => provenance.TrackId is not null || provenance.SourceAssetIds.Count > 0;

    private static Placement Build(PlacementLevel? artist, PlacementLevel? album, PlacementLevel? song, int? year) => new()
    {
        Artist = artist ?? Fallback(FolderLevel.Artist),
        Album = album ?? Fallback(FolderLevel.Album),
        Song = song ?? Fallback(FolderLevel.Song),
        Year = year,
    };

    private static PlacementLevel? Pick(FolderLevel level, (string? Name, PlacementSource Source)[] candidates)
    {
        foreach (var (name, source) in candidates)
        {
            if (LibraryKey.IsMeaningful(name))
            {
                return PlacementLevel.Of(level, name!, source);
            }
        }

        return null;
    }

    private static PlacementLevel Fallback(FolderLevel level) => PlacementLevel.Of(level, LibraryKey.FallbackOf(level), PlacementSource.Fallback);

    private static int? YearOf(int? year) => year is >= Track.MinYear and <= Track.MaxYear ? year : null;
}
