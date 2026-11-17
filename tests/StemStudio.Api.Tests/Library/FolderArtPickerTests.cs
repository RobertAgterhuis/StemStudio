using StemStudio.Api.Library;
using StemStudio.Contracts.Library;
using StemStudio.Domain.Library;
using StemStudio.Domain.Tracks;
using static StemStudio.Api.Tests.Library.SnapshotBuilder;

namespace StemStudio.Api.Tests.Library;

/// <summary>022 T040: folder art (research R-05, FR-015, FR-016, SC-005).</summary>
public sealed class FolderArtPickerTests
{
    private static readonly Guid[] Tracks = [.. Enumerable.Range(1, 6).Select(i => Guid.Parse($"0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0{i}"))];

    private static LibraryFolderDto Open(LibrarySnapshot snapshot, string id) => FolderTreeBuilder.View(snapshot, new FolderQuery { FolderId = id })!.Folder!;

    [Fact]
    public void SongFolder_UsesItsTracksPicture()
    {
        var builder = new SnapshotBuilder().Track(Tracks[0], ArtworkSource.Spotify, "aaaaaaaaaaaa");
        var item = builder.Export(Tracks[0], Matched("A", "B", "C"));

        var art = Open(builder.Build(), item.Placement!.SongFolderId).Art;

        (art.Kind, art.Images.Single().ThumbUrl, art.Images.Single().CoverUrl)
            .ShouldBe((FolderArtKindDto.Cover, $"/api/tracks/{Tracks[0]}/artwork/thumb?v=aaaaaaaaaaaa", $"/api/tracks/{Tracks[0]}/artwork/cover?v=aaaaaaaaaaaa"));
        art.Placeholder.Glyph.ShouldBe("C");
    }

    [Fact]
    public void AlbumFolder_BestRankThenLatestTrack()
    {
        var builder = new SnapshotBuilder()
            .Track(Tracks[0], ArtworkSource.Youtube, "111111111111", minutes: 9)
            .Track(Tracks[1], ArtworkSource.Spotify, "222222222222", minutes: 1)
            .Track(Tracks[2], ArtworkSource.Spotify, "333333333333", minutes: 5);
        var item = builder.Export(Tracks[0], Matched("A", "Album", "One"));
        builder.Export(Tracks[1], Matched("A", "Album", "Two"));
        builder.Export(Tracks[2], Matched("A", "Album", "Three"));

        Open(builder.Build(), item.Placement!.AlbumFolderId).Art.Images.Single().ThumbUrl.ShouldContain("333333333333");
    }

    [Fact]
    public void AlbumFolder_UserPictureRanksFirst()
    {
        var builder = new SnapshotBuilder().Track(Tracks[0], ArtworkSource.Spotify, "111111111111", 9).Track(Tracks[1], ArtworkSource.User, "222222222222");
        var item = builder.Export(Tracks[0], Matched("A", "Album", "One"));
        builder.Export(Tracks[1], Matched("A", "Album", "Two"));

        Open(builder.Build(), item.Placement!.AlbumFolderId).Art.Images.Single().ThumbUrl.ShouldContain("222222222222");
    }

    [Theory]
    [InlineData(0, FolderArtKindDto.Placeholder)]
    [InlineData(1, FolderArtKindDto.Cover)]
    [InlineData(2, FolderArtKindDto.Mosaic)]
    [InlineData(5, FolderArtKindDto.Mosaic)]
    public void ArtistFolder_MosaicOfUpToFourAlbumCovers(int albums, FolderArtKindDto kind)
    {
        var builder = new SnapshotBuilder();
        var first = builder.Export(Tracks[5], Matched("Band", "No cover", "x"));
        for (var i = 0; i < albums; i++)
        {
            builder.Track(Tracks[i], ArtworkSource.Spotify, $"{i}{i}{i}{i}{i}{i}{i}{i}{i}{i}{i}{i}");
            builder.Export(Tracks[i], Matched("Band", $"Album {i}", "Song", 2000 + i));
        }

        var art = Open(builder.Build(), first.Placement!.ArtistFolderId).Art;

        (art.Kind, art.Images.Count).ShouldBe((kind, Math.Min(albums, 4)));
        if (albums > 1)
        {
            art.Images[0].ThumbUrl.ShouldContain("000000000000");
        }
    }

    [Fact]
    public void ArtistFolder_TheSameCoverCountsOnce()
    {
        var builder = new SnapshotBuilder().Track(Tracks[0], ArtworkSource.Spotify, "aaaaaaaaaaaa");
        var item = builder.Export(Tracks[0], Matched("Band", "One", "x"));
        builder.Export(Tracks[0], Matched("Band", "Two", "y"));

        Open(builder.Build(), item.Placement!.ArtistFolderId).Art.Kind.ShouldBe(FolderArtKindDto.Cover);
    }

    [Fact]
    public void DeletedTrackPictures_AreAbsent()
    {
        var builder = new SnapshotBuilder();
        var item = builder.Export(Tracks[0], Matched("A", "B", "C"));

        var art = Open(builder.Build(), item.Placement!.SongFolderId).Art;

        (art.Kind, art.Images.Count).ShouldBe((FolderArtKindDto.Placeholder, 0));
    }

    [Theory]
    [InlineData("björk", PlacementSource.Match, "B")]
    [InlineData("\"Weird Al\" Yankovic", PlacementSource.Import, "W")]
    [InlineData("ддт", PlacementSource.Import, "Д")]
    [InlineData("2Pac", PlacementSource.Import, "2")]
    [InlineData("!!!", PlacementSource.Item, "?")]
    [InlineData(LibraryKey.UnknownArtist, PlacementSource.Fallback, "?")]
    [InlineData(LibraryKey.OwnSongsArtist, PlacementSource.Fallback, "♪")]
    [InlineData(LibraryKey.OwnSongsAlbum, PlacementSource.Fallback, "♪")]
    public void Placeholder_Glyph(string name, PlacementSource source, string glyph) =>
        FolderArtPicker.Placeholder("ar0123456789abcdef", name, source).Glyph.ShouldBe(glyph);

    [Fact]
    public void Placeholder_ToneFromTheFolderId()
    {
        FolderArtPicker.Placeholder("ar00000000000000ff", "x", PlacementSource.Import).Tone.ShouldBe(7);
        FolderArtPicker.Placeholder("ar0000000000000008", "x", PlacementSource.Import).Tone.ShouldBe(0);
        Enumerable.Range(0, 50).Select(i => FolderArtPicker.Placeholder(FolderId.Artist($"k{i}"), "x", PlacementSource.Import).Tone).ShouldAllBe(tone => tone >= 0 && tone < 8);
    }

    [Fact]
    public void Guards_RejectNull()
    {
        var snapshot = new SnapshotBuilder().Build();
        Should.Throw<ArgumentNullException>(() => FolderArtPicker.BestImage(null!, []));
        Should.Throw<ArgumentNullException>(() => FolderArtPicker.BestImage(snapshot, null!));
        Should.Throw<ArgumentNullException>(() => FolderArtPicker.Art(null!, "ar0123456789abcdef", "x", PlacementSource.Item));
        Should.Throw<ArgumentNullException>(() => FolderArtPicker.Placeholder(null!, "x", PlacementSource.Item));
        Should.Throw<ArgumentNullException>(() => FolderArtPicker.Placeholder("ar0123456789abcdef", null!, PlacementSource.Item));
    }
}
