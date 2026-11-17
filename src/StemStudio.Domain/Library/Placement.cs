namespace StemStudio.Domain.Library;

/// <summary>Where a library item is filed (feature 022, data-model 2.3): one level each for artist, album and song.</summary>
public sealed record Placement
{
    /// <summary>Artist level.</summary>
    public required PlacementLevel Artist { get; init; }

    /// <summary>Album level.</summary>
    public required PlacementLevel Album { get; init; }

    /// <summary>Song level.</summary>
    public required PlacementLevel Song { get; init; }

    /// <summary>Album year (1000..2999) from the match; null otherwise.</summary>
    public int? Year { get; init; }

    /// <summary>Id of the artist folder.</summary>
    public string ArtistFolderId => FolderId.Artist(Artist.Key);

    /// <summary>Id of the album folder.</summary>
    public string AlbumFolderId => FolderId.Album(Artist.Key, Album.Key);

    /// <summary>Id of the song folder.</summary>
    public string SongFolderId => FolderId.Song(Artist.Key, Album.Key, Song.Key);

    /// <summary>The placement level of a folder level.</summary>
    /// <param name="level">Folder level.</param>
    /// <returns>The placement level.</returns>
    public PlacementLevel Of(FolderLevel level) => level switch
    {
        FolderLevel.Artist => Artist,
        FolderLevel.Album => Album,
        _ => Song,
    };

    /// <summary>The folder id of a level.</summary>
    /// <param name="level">Folder level.</param>
    /// <returns>The folder id.</returns>
    public string FolderIdOf(FolderLevel level) => level switch
    {
        FolderLevel.Artist => ArtistFolderId,
        FolderLevel.Album => AlbumFolderId,
        _ => SongFolderId,
    };
}
