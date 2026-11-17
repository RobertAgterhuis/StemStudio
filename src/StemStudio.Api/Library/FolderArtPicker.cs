using System.Globalization;
using StemStudio.Contracts;
using StemStudio.Contracts.Library;
using StemStudio.Domain.Library;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Library;

/// <summary>
/// Folder art (feature 022, research R-05, FR-015/FR-016): song = its track's picture, album = the best picture among its
/// songs' tracks (005 rank, ties the latest changed track), artist = up to four distinct album covers in album order; else
/// a lettered placeholder. Only existing 005 artwork URLs are referenced. Pure.
/// </summary>
public static class FolderArtPicker
{
    /// <summary>Glyph of "My songs".</summary>
    public const string NoteGlyph = "♪";

    /// <summary>Glyph of unknown folders.</summary>
    public const string UnknownGlyph = "?";

    /// <summary>The best picture of a set of items (song and album folders).</summary>
    /// <param name="snapshot">The snapshot (existing tracks).</param>
    /// <param name="entries">The items of the folder.</param>
    /// <returns>The image, or null.</returns>
    public static ArtImageDto? BestImage(LibrarySnapshot snapshot, IEnumerable<LibraryEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(entries);
        var best = entries.Select(entry => entry.Asset.TrackId).OfType<Guid>().Distinct()
            .Select(id => snapshot.Tracks.GetValueOrDefault(id)).OfType<TrackArtRow>().Where(track => track.Artwork is not null)
            .OrderBy(track => ArtworkPrecedence.Rank(track.Artwork!.Source)).ThenByDescending(track => track.UpdatedAt).ThenBy(track => track.TrackId)
            .FirstOrDefault();
        return best is null ? null : Image(best.TrackId, best.Artwork!);
    }

    /// <summary>The art of a folder.</summary>
    /// <param name="images">Song/album: at most one image; artist: the album covers in album order.</param>
    /// <param name="folderId">Folder id (the tone is derived from it).</param>
    /// <param name="name">Display name.</param>
    /// <param name="nameSource">Source of the display name.</param>
    /// <returns>The art.</returns>
    public static FolderArtDto Art(IEnumerable<ArtImageDto> images, string folderId, string name, PlacementSource nameSource)
    {
        ArgumentNullException.ThrowIfNull(images);
        var distinct = images.DistinctBy(image => image.ThumbUrl).Take(ContractLimits.FolderArtMosaicMax).ToList();
        var kind = distinct.Count switch
        {
            0 => FolderArtKindDto.Placeholder,
            1 => FolderArtKindDto.Cover,
            _ => FolderArtKindDto.Mosaic,
        };
        return new FolderArtDto { Kind = kind, Images = distinct, Placeholder = Placeholder(folderId, name, nameSource) };
    }

    /// <summary>The lettered placeholder: first letter or digit (upper case), <c>?</c> for unknown, a note for "My songs".</summary>
    /// <param name="folderId">Folder id.</param>
    /// <param name="name">Display name.</param>
    /// <param name="nameSource">Source of the display name.</param>
    /// <returns>The placeholder.</returns>
    public static ArtPlaceholderDto Placeholder(string folderId, string name, PlacementSource nameSource)
    {
        ArgumentNullException.ThrowIfNull(folderId);
        ArgumentNullException.ThrowIfNull(name);
        return new ArtPlaceholderDto { Glyph = Glyph(name, nameSource), Tone = Convert.ToInt32(folderId[^2..], 16) % ContractLimits.ArtToneCount };
    }

    private static string Glyph(string name, PlacementSource nameSource)
    {
        if (nameSource == PlacementSource.Fallback)
        {
            return name is LibraryKey.OwnSongsArtist or LibraryKey.OwnSongsAlbum ? NoteGlyph : UnknownGlyph;
        }

        var elements = StringInfo.GetTextElementEnumerator(name);
        while (elements.MoveNext())
        {
            var element = elements.GetTextElement();
            if (char.IsLetterOrDigit(element, 0))
            {
                return element.ToUpperInvariant();
            }
        }

        return UnknownGlyph;
    }

    private static ArtImageDto Image(Guid trackId, TrackArtwork artwork) => new()
    {
        ThumbUrl = ApiRoutes.TrackArtwork(trackId, ApiRoutes.ArtworkThumb, artwork.Version),
        CoverUrl = ApiRoutes.TrackArtwork(trackId, ApiRoutes.ArtworkCover, artwork.Version),
    };
}
