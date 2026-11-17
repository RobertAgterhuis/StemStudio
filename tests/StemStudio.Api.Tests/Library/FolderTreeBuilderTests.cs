using StemStudio.Api.Library;
using StemStudio.Contracts.Library;
using StemStudio.Domain.Assets;
using StemStudio.Domain.Library;
using StemStudio.Domain.Scores;
using StemStudio.Domain.Tracks;
using static StemStudio.Api.Tests.Library.SnapshotBuilder;

namespace StemStudio.Api.Tests.Library;

/// <summary>022 T040: folder views from a snapshot (FR-007, FR-011, FR-013..FR-018).</summary>
public sealed class FolderTreeBuilderTests
{
    private static readonly Guid TrackA = Guid.Parse("0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a");
    private static readonly Guid TrackB = Guid.Parse("5d1c2b3a-4e5f-4a6b-8c7d-9e0f1a2b3c4d");
    private static readonly Guid TrackC = Guid.Parse("6d1c2b3a-4e5f-4a6b-8c7d-9e0f1a2b3c4d");

    private static LibraryFolderViewDto View(LibrarySnapshot snapshot, FolderQuery? query = null) => FolderTreeBuilder.View(snapshot, query ?? new FolderQuery())!;

    private static LibraryFolderViewDto Open(LibrarySnapshot snapshot, string id, FolderQuery? query = null) =>
        FolderTreeBuilder.View(snapshot, (query ?? new FolderQuery()) with { FolderId = id })!;

    [Fact]
    public void Root_ListsArtistsAToZWithoutTheAndUnknownLast()
    {
        var builder = new SnapshotBuilder();
        builder.Export(TrackA, Imported("The Beatles", "Help"));
        builder.Export(TrackA, Imported("ABBA", "SOS"));
        builder.Export(TrackA, Imported("🎸", "Noise"));
        builder.Generated("Zomeravond");
        builder.Export(TrackA, Imported("Coldplay", "Yellow"));

        var view = View(builder.Build());

        view.Children.Select(child => child.Name).ShouldBe(["ABBA", "The Beatles", "Coldplay", LibraryKey.OwnSongsArtist, LibraryKey.UnknownArtist]);
        (view.Folder, view.Breadcrumb.Count, view.ChildrenTotal, view.TotalItems, view.Revision).ShouldBe((null, 0, 5, 5, Revision));
        view.Children.Single(child => child.Name == LibraryKey.UnknownArtist).Art.Placeholder.Glyph.ShouldBe(FolderArtPicker.UnknownGlyph);
        view.Children.Single(child => child.Name == LibraryKey.OwnSongsArtist).Art.Placeholder.Glyph.ShouldBe(FolderArtPicker.NoteGlyph);
    }

    [Fact]
    public void Root_CountsAlbumsSongsItemsAndSize()
    {
        var builder = new SnapshotBuilder();
        builder.Export(TrackA, Matched("Metallica", "Metallica", "Enter Sandman", 1991), size: 100);
        builder.Export(TrackA, Matched("Metallica", "Metallica", "Enter Sandman", 1991), "bass", size: 200);
        builder.Export(TrackB, Matched("Metallica", "Master of Puppets", "Battery", 1986), size: 300);

        var artist = View(builder.Build()).Children.Single();

        (artist.Counts.Albums, artist.Counts.Songs, artist.Counts.Items, artist.SizeBytes, artist.NameSource).ShouldBe((2, 2, 3, 600L, PlacementSourceDto.Match));
        (artist.Level, artist.Year, artist.TrackIds.Count).ShouldBe((FolderLevelDto.Artist, null, 0));
    }

    [Fact]
    public void Paging_LimitAndOffset()
    {
        var builder = new SnapshotBuilder();
        foreach (var name in new[] { "A", "B", "C", "D" })
        {
            builder.Export(TrackA, Imported(name, "Song"));
        }

        var view = View(builder.Build(), new FolderQuery { Limit = 2, Offset = 1 });

        (view.ChildrenTotal, string.Join(",", view.Children.Select(child => child.Name))).ShouldBe((4, "B,C"));
    }

    [Fact]
    public void DisplayName_PrefersTheMatchSpelling()
    {
        var builder = new SnapshotBuilder();
        builder.Export(TrackA, Imported("metallica", "One"));
        builder.Export(TrackA, Imported("metallica", "One"));
        builder.Export(TrackB, Matched("Metallica", "x", "One"));

        View(builder.Build()).Children.Single().Name.ShouldBe("Metallica");
    }

    [Fact]
    public void DisplayName_MostUsedThenEarliest()
    {
        var builder = new SnapshotBuilder();
        builder.Export(TrackA, Imported("ABBA", "SOS"));
        builder.Export(TrackA, Imported("abba", "SOS"));
        builder.Export(TrackA, Imported("abba", "SOS"));
        var tie = new SnapshotBuilder();
        tie.Export(TrackA, Imported("Björk", "Joga"));
        tie.Export(TrackA, Imported("Bjork", "Joga"));

        View(builder.Build()).Children.Single().Name.ShouldBe("abba");
        View(tie.Build()).Children.Single().Name.ShouldBe("Björk");
    }

    [Fact]
    public void ArtistFolder_AlbumsByYearUnknownLast()
    {
        var builder = new SnapshotBuilder();
        var first = builder.Export(TrackA, Matched("Metallica", "Metallica", "One", 1991));
        builder.Export(TrackB, Matched("Metallica", "Kill 'Em All", "Seek", 1983));
        builder.Export(TrackC, Imported("Metallica", "Live song"));

        var view = Open(builder.Build(), first.Placement!.ArtistFolderId);

        view.Children.Select(album => (album.Name, album.Year)).ShouldBe([("Kill 'Em All", 1983), ("Metallica", 1991), (LibraryKey.UnknownAlbum, null)]);
        (view.Folder!.Name, view.Breadcrumb.Count).ShouldBe(("Metallica", 0));
    }

    [Fact]
    public void ArtistFolder_NameSortAndYearSort()
    {
        var builder = new SnapshotBuilder();
        var first = builder.Export(TrackA, Matched("Band", "The Zebra", "One", 1999));
        builder.Export(TrackB, Matched("Band", "Apple", "Two", 2005));

        var byName = Open(builder.Build(), first.Placement!.ArtistFolderId, new FolderQuery { Sort = FolderSortDto.Name });

        byName.Children.Select(album => album.Name).ShouldBe(["Apple", "The Zebra"]);
    }

    [Fact]
    public void AlbumFolder_SongsAToZWithBreadcrumb()
    {
        var builder = new SnapshotBuilder();
        var one = builder.Export(TrackA, Matched("Metallica", "Master of Puppets", "The Thing That Should Not Be"));
        builder.Export(TrackB, Matched("Metallica", "Master of Puppets", "Battery"));

        var view = Open(builder.Build(), one.Placement!.AlbumFolderId);

        view.Children.Select(song => song.Name).ShouldBe(["Battery", "The Thing That Should Not Be"]);
        view.Breadcrumb.Select(crumb => (crumb.Name, crumb.Level)).ShouldBe([("Metallica", FolderLevelDto.Artist)]);
        (view.Folder!.Counts.Albums, view.Folder.Counts.Songs, view.Folder.Counts.Items).ShouldBe((null, 2, 2));
    }

    [Fact]
    public void SongFolder_ItemsInMixerOrderThenNewestFirst()
    {
        var builder = new SnapshotBuilder().Track(TrackA);
        var facts = Matched("Metallica", "Master of Puppets", "Battery");
        var mix = builder.Mix(TrackA, facts, "drums", "bass");
        var score = builder.Score(TrackA, facts);
        var snare = builder.Export(TrackA, facts, "drums.snare");
        var bass = builder.Export(TrackA, facts, "bass");
        var drums = builder.Export(TrackA, facts, "drums");
        var newerDrums = builder.Export(TrackA, facts, "drums");

        var view = Open(builder.Build(), drums.Placement!.SongFolderId);

        view.Items.Select(item => item.Id).ShouldBe([newerDrums.Id, drums.Id, bass.Id, snare.Id, mix.Id, score.Id]);
        (view.Children.Count, view.ChildrenTotal, view.Folder!.Counts.Songs, view.Folder.TrackIds.Single()).ShouldBe((0, 0, null, TrackA));
        view.Breadcrumb.Select(crumb => crumb.Level).ShouldBe([FolderLevelDto.Artist, FolderLevelDto.Album]);
        view.Items[0].Provenance.TrackAvailable.ShouldBeTrue();
    }

    [Fact]
    public void SongFolder_DeletedTracksAreNotLinked()
    {
        var builder = new SnapshotBuilder();
        var item = builder.Export(TrackA, Matched("A", "B", "C"));

        var view = Open(builder.Build(), item.Placement!.SongFolderId);

        (view.Folder!.TrackIds.Count, view.Items.Single().Provenance.TrackAvailable).ShouldBe((0, false));
    }

    [Fact]
    public void Filters_DropFoldersWithoutMatchingItems()
    {
        var builder = new SnapshotBuilder();
        builder.Score(TrackA, Matched("Pianist", "Album", "Etude"), ScoreProfile.Piano);
        builder.Mix(TrackB, Matched("Drummer", "Album", "Groove"), "drums");

        var view = View(builder.Build(), new FolderQuery { Kind = AssetKind.Score });
        var queued = View(builder.Build(), new FolderQuery { Status = AssetStatus.Queued });
        var ready = View(builder.Build(), new FolderQuery { Status = AssetStatus.Ready });

        view.Children.Single().Name.ShouldBe("Pianist");
        (queued.ChildrenTotal, ready.ChildrenTotal, ready.TotalItems).ShouldBe((2, 0, 0));
    }

    [Fact]
    public void FilteredFolder_KeepsItsNameAndShowsZeroItems()
    {
        var builder = new SnapshotBuilder();
        var item = builder.Mix(TrackA, Matched("Drummer", "Album", "Groove"), "drums");

        var view = Open(builder.Build(), item.Placement!.ArtistFolderId, new FolderQuery { Status = AssetStatus.Ready });

        (view.Folder!.Name, view.Folder.Counts.Items, view.Folder.Counts.Albums, view.Children.Count).ShouldBe(("Drummer", 0, null, 0));
    }

    [Theory]
    [InlineData(FolderSortDto.Recent, "C,B,A")]
    [InlineData(FolderSortDto.Size, "B,C,A")]
    [InlineData(FolderSortDto.Items, "A,B,C")]
    [InlineData(FolderSortDto.Year, "A,B,C")]
    public void AdvancedSorts(FolderSortDto sort, string expected)
    {
        var builder = new SnapshotBuilder();
        builder.Export(TrackA, Imported("A", "x"), size: 1);
        builder.Export(TrackA, Imported("A", "y"), size: 1);
        builder.Export(TrackA, Imported("B", "x"), size: 500);
        builder.Export(TrackA, Imported("C", "x"), size: 10);

        var view = View(builder.Build(), new FolderQuery { Sort = sort });

        string.Join(",", view.Children.Select(child => child.Name)).ShouldBe(expected);
    }

    [Fact]
    public void UnknownOrEmptiedFolder_IsNull()
    {
        var snapshot = new SnapshotBuilder().Build();

        FolderTreeBuilder.View(snapshot, new FolderQuery { FolderId = FolderId.Artist("nobody") }).ShouldBeNull();
        FolderTreeBuilder.View(snapshot, new FolderQuery { FolderId = "not-an-id" }).ShouldBeNull();
        View(snapshot).Children.ShouldBeEmpty();
    }

    [Fact]
    public void UnfiledItems_AppearWithTheirPreviewAndCountAsPending()
    {
        var builder = new SnapshotBuilder();
        builder.Unfiled(TrackA, Imported("Rick Astley", "Never Gonna Give You Up"));

        var view = View(builder.Build());

        (view.PendingFiling, view.Children.Single().Name).ShouldBe((1, "Rick Astley"));
    }

    [Fact]
    public void Defaults_PerLevel()
    {
        FolderSorting.DefaultFor(FolderLevel.Artist).ShouldBe(FolderSortDto.Name);
        FolderSorting.DefaultFor(FolderLevel.Album).ShouldBe(FolderSortDto.Year);
        FolderSorting.DefaultFor(FolderLevel.Song).ShouldBe(FolderSortDto.Name);
    }

    [Fact]
    public void Guards_RejectNull()
    {
        var snapshot = new SnapshotBuilder().Build();
        Should.Throw<ArgumentNullException>(() => FolderTreeBuilder.View(null!, new FolderQuery()));
        Should.Throw<ArgumentNullException>(() => FolderTreeBuilder.View(snapshot, null!));
        Should.Throw<ArgumentNullException>(() => FolderSorting.Sort(null!, FolderLevel.Artist, FolderSortDto.Name));
        Should.Throw<ArgumentNullException>(() => FolderTreeBuilder.DisplayName(null!, FolderLevel.Artist));
        Should.Throw<ArgumentNullException>(() => LibrarySnapshot.Build(Revision, null!, []));
        Should.Throw<ArgumentNullException>(() => InstrumentTerms.Of(null!));
        _ = ArtworkSource.User;
    }
}
