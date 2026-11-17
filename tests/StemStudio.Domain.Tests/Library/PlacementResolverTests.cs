using StemStudio.Domain.Assets;
using StemStudio.Domain.Library;
using StemStudio.Domain.Tracks;

namespace StemStudio.Domain.Tests.Library;

/// <summary>022 T014: placement precedence per level (research R-01, FR-001, FR-003, FR-006, FR-008).</summary>
public sealed class PlacementResolverTests
{
    private static readonly Guid TrackId = Guid.Parse("0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a");

    public static Provenance Provenance(Guid? trackId = null, string title = "Enter Sandman (Official Video)", string? artist = "MetallicaTV", Guid[]? sources = null) => new()
    {
        Origin = AssetOrigin.Export,
        TrackId = trackId,
        TrackTitle = title,
        TrackArtist = artist,
        Contents = "drums, raw",
        SourceAssetIds = sources ?? [],
    };

    public static TrackFacts Found(string artist = "Metallica", string album = "Metallica", string song = "Enter Sandman", int? year = 1991) => new(
        "Enter Sandman (Official Video)",
        "MetallicaTV",
        new TrackMetadata { Status = MetadataStatus.Found, ArtistName = artist, Album = album, RecordingTitle = song, Year = year, LookupTitle = "Enter Sandman", LookupArtist = "Metallica", LookupAlbum = "Black Album" },
        null);

    public static TrackFacts Imported(MetadataStatus status = MetadataStatus.NotFound, string? lookupAlbum = null) => new(
        "Enter Sandman (Official Video)",
        "MetallicaTV",
        new TrackMetadata { Status = status, LookupTitle = "Enter Sandman", LookupArtist = "Metallica", LookupAlbum = lookupAlbum, ArtistName = "Ignored", Album = "Ignored" },
        null);

    private static Placement Resolve(PlacementInput input) => PlacementResolver.Resolve(input);

    private static (string, PlacementSource) Level(PlacementLevel level) => (level.Name, level.Source);

    [Fact]
    public void Match_WinsOverImport()
    {
        var placement = Resolve(new PlacementInput { Provenance = Provenance(TrackId), Track = Found() });

        Level(placement.Artist).ShouldBe(("Metallica", PlacementSource.Match));
        Level(placement.Album).ShouldBe(("Metallica", PlacementSource.Match));
        Level(placement.Song).ShouldBe(("Enter Sandman", PlacementSource.Match));
        placement.Year.ShouldBe(1991);
    }

    [Theory]
    [InlineData(MetadataStatus.NotFound)]
    [InlineData(MetadataStatus.Failed)]
    [InlineData(MetadataStatus.Pending)]
    [InlineData(MetadataStatus.None)]
    public void NotFoundOrFailedLookups_UseTheImportNames(MetadataStatus status)
    {
        var placement = Resolve(new PlacementInput { Provenance = Provenance(TrackId), Track = Imported(status, "Black Album") });

        Level(placement.Artist).ShouldBe(("Metallica", PlacementSource.Import));
        Level(placement.Album).ShouldBe(("Black Album", PlacementSource.Import));
        Level(placement.Song).ShouldBe(("Enter Sandman", PlacementSource.Import));
        placement.Year.ShouldBeNull();
    }

    [Fact]
    public void NoLookupNames_UseTheTrackNamesThenFallback()
    {
        var track = new TrackFacts("Raw title", "Raw artist", TrackMetadata.None, null);

        var placement = Resolve(new PlacementInput { Provenance = Provenance(TrackId), Track = track });

        Level(placement.Artist).ShouldBe(("Raw artist", PlacementSource.Import));
        Level(placement.Album).ShouldBe((LibraryKey.UnknownAlbum, PlacementSource.Fallback));
        Level(placement.Song).ShouldBe(("Raw title", PlacementSource.Import));
    }

    [Fact]
    public void NoArtistAnywhere_UsesUnknownArtist()
    {
        var track = new TrackFacts("🎵", null, TrackMetadata.None, null);

        var placement = Resolve(new PlacementInput { Provenance = Provenance(TrackId, title: "!!!", artist: null), Track = track });

        Level(placement.Artist).ShouldBe((LibraryKey.UnknownArtist, PlacementSource.Fallback));
        Level(placement.Song).ShouldBe((LibraryKey.UntitledSong, PlacementSource.Fallback));
    }

    [Fact]
    public void Levels_AreIndependent()
    {
        var input = new PlacementInput { Provenance = Provenance(TrackId), Track = Found(), TrackInfo = new LibraryInfo { Album = "My album" }, ItemInfo = new LibraryInfo { Song = "My take" } };

        var placement = Resolve(input);

        Level(placement.Artist).ShouldBe(("Metallica", PlacementSource.Match));
        Level(placement.Album).ShouldBe(("My album", PlacementSource.Track));
        Level(placement.Song).ShouldBe(("My take", PlacementSource.Item));
        placement.Year.ShouldBeNull();
    }

    [Fact]
    public void ItemInfo_WinsOverTrackInfo()
    {
        var input = new PlacementInput { Provenance = Provenance(TrackId), Track = Found(), TrackInfo = new LibraryInfo { Artist = "Track" }, ItemInfo = new LibraryInfo { Artist = "Item" } };

        Level(Resolve(input).Artist).ShouldBe(("Item", PlacementSource.Item));
    }

    [Fact]
    public void VariousArtists_IsIgnoredByName()
    {
        var placement = Resolve(new PlacementInput { Provenance = Provenance(TrackId), Track = Found(artist: "Various Artists") });

        Level(placement.Artist).ShouldBe(("Metallica", PlacementSource.Import));
        Level(placement.Album).ShouldBe(("Metallica", PlacementSource.Match));
    }

    [Fact]
    public void VariousArtists_IsIgnoredByMbid()
    {
        var placement = Resolve(new PlacementInput { Provenance = Provenance(TrackId), Track = Found(artist: "VA") with { ArtistMbid = LibraryKey.VariousArtistsMbid } });

        placement.Artist.Source.ShouldBe(PlacementSource.Import);
    }

    [Theory]
    [InlineData(999)]
    [InlineData(3000)]
    [InlineData(null)]
    public void Year_OutOfRangeIsDropped(int? year) =>
        Resolve(new PlacementInput { Provenance = Provenance(TrackId), Track = Found(year: year) }).Year.ShouldBeNull();

    [Fact]
    public void DeletedTrack_UsesTheProvenanceSnapshot()
    {
        var placement = Resolve(new PlacementInput { Provenance = Provenance(TrackId) });

        Level(placement.Artist).ShouldBe(("MetallicaTV", PlacementSource.Import));
        Level(placement.Album).ShouldBe((LibraryKey.UnknownAlbum, PlacementSource.Fallback));
        Level(placement.Song).ShouldBe(("Enter Sandman (Official Video)", PlacementSource.Import));
    }

    [Fact]
    public void DeletedTrack_KeepsThePreviousPlacement()
    {
        var previous = Resolve(new PlacementInput { Provenance = Provenance(TrackId), Track = Found() });

        Resolve(new PlacementInput { Provenance = Provenance(TrackId), Previous = previous }).ShouldBe(previous);
    }

    [Fact]
    public void DeletedTrack_ItemLevelsOfThePreviousPlacementFollowTheItemInfo()
    {
        var previous = Resolve(new PlacementInput { Provenance = Provenance(TrackId), Track = Found(), ItemInfo = new LibraryInfo { Artist = "Mine", Album = "Mine" } });

        var placement = Resolve(new PlacementInput { Provenance = Provenance(TrackId), Previous = previous, ItemInfo = new LibraryInfo { Album = "Mine" } });

        Level(placement.Artist).ShouldBe(("MetallicaTV", PlacementSource.Import));
        Level(placement.Album).ShouldBe(("Mine", PlacementSource.Item));
        Level(placement.Song).ShouldBe(("Enter Sandman", PlacementSource.Match));
    }

    [Fact]
    public void ScoreFromAnItem_UsesTheSourcePlacement()
    {
        var source = Resolve(new PlacementInput { Provenance = Provenance(TrackId), Track = Found() });

        var placement = Resolve(new PlacementInput { Provenance = Provenance(sources: [Guid.NewGuid()]), SourcePlacement = source });

        placement.ShouldBe(source);
    }

    [Fact]
    public void ScoreFromAnItem_ItemInfoStillWins()
    {
        var source = Resolve(new PlacementInput { Provenance = Provenance(TrackId), Track = Found() });

        var placement = Resolve(new PlacementInput { Provenance = Provenance(sources: [Guid.NewGuid()]), SourcePlacement = source, ItemInfo = new LibraryInfo { Album = "Other" } });

        Level(placement.Album).ShouldBe(("Other", PlacementSource.Item));
        placement.Year.ShouldBeNull();
    }

    [Fact]
    public void NoTrackAndNoSource_IsFiledUnderMySongs()
    {
        var placement = Resolve(new PlacementInput { Provenance = Provenance(title: "Zomeravond", artist: null) });

        Level(placement.Artist).ShouldBe((LibraryKey.OwnSongsArtist, PlacementSource.Fallback));
        Level(placement.Album).ShouldBe((LibraryKey.OwnSongsAlbum, PlacementSource.Fallback));
        Level(placement.Song).ShouldBe(("Zomeravond", PlacementSource.Import));
    }

    [Fact]
    public void NoTrackAndNoSource_IgnoresAPreviousPlacement()
    {
        var previous = Resolve(new PlacementInput { Provenance = Provenance(TrackId), Track = Found() });

        Resolve(new PlacementInput { Provenance = Provenance(title: "Zomeravond"), Previous = previous }).Artist.Name.ShouldBe(LibraryKey.OwnSongsArtist);
    }

    [Fact]
    public void LongNames_AreCutTo200()
    {
        var placement = Resolve(new PlacementInput { Provenance = Provenance(TrackId), Track = Found(song: new string('s', 500)) });

        placement.Song.Name.Length.ShouldBe(200);
        placement.Song.Key.ShouldBe(LibraryKey.For(FolderLevel.Song, placement.Song.Name));
    }

    [Fact]
    public void SameNames_GiveTheSameFolders()
    {
        var a = Resolve(new PlacementInput { Provenance = Provenance(TrackId), Track = Found() });
        var b = Resolve(new PlacementInput { Provenance = Provenance(TrackId), TrackInfo = new LibraryInfo { Artist = "METALLICA", Album = "metallica", Song = "enter  sandman" }, Track = Found() });

        b.SongFolderId.ShouldBe(a.SongFolderId);
        b.FolderIdOf(FolderLevel.Artist).ShouldBe(a.ArtistFolderId);
        b.FolderIdOf(FolderLevel.Album).ShouldBe(a.AlbumFolderId);
        b.FolderIdOf(FolderLevel.Song).ShouldBe(a.SongFolderId);
    }

    [Fact]
    public void Automatic_IgnoresUserNames()
    {
        var input = new PlacementInput { Provenance = Provenance(TrackId), Track = Found(), ItemInfo = new LibraryInfo { Artist = "Mine" } };

        PlacementResolver.Automatic(input).Artist.Name.ShouldBe("Metallica");
    }

    [Fact]
    public void Resolve_Null_Throws()
    {
        Should.Throw<ArgumentNullException>(() => PlacementResolver.Resolve(null!));
        Should.Throw<ArgumentNullException>(() => PlacementResolver.Automatic(null!));
        Should.Throw<ArgumentNullException>(() => TrackFacts.From(null!));
    }
}
