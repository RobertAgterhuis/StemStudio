using StemStudio.Api.Library;
using StemStudio.Contracts.Library;
using StemStudio.Domain.Assets;
using static StemStudio.Api.Tests.Library.SnapshotBuilder;

namespace StemStudio.Api.Tests.Library;

/// <summary>022 T060: the live search (research R-07, FR-022..FR-025, FR-029, FR-036, SC-006).</summary>
public sealed class LibrarySearchTests
{
    private static readonly Guid TrackA = Guid.Parse("0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a");

    private static readonly string[] Channels =
        ["vocals", "vocals.lead", "vocals.backing", "drums", "drums.snare", "drums.hihat", "bass", "guitar", "guitar.acoustic-guitar", "piano", "piano.keys", "piano.synth", "other", "other.strings", "other.brass"];

    private static LibrarySearchResultDto Search(LibrarySnapshot snapshot, string text, string? within = null, int limit = 20) =>
        LibrarySearch.Run(snapshot, new SearchRequest(text, LibrarySearch.Tokenize(text), within, limit))!;

    private static (LibrarySnapshot Snapshot, Dictionary<string, Asset> Items) InstrumentLibrary()
    {
        var builder = new SnapshotBuilder();
        var items = Channels.ToDictionary(channel => channel, channel => builder.Export(TrackA, Imported("Zed", "Tune"), channel));
        return (builder.Build(), items);
    }

    private static string[] ChannelsFound(LibrarySnapshot snapshot, Dictionary<string, Asset> items, string text)
    {
        var ids = Search(snapshot, text, limit: 200).Items.Select(hit => hit.Asset.Id).ToHashSet();
        return [.. items.Where(pair => ids.Contains(pair.Value.Id)).Select(pair => pair.Key)];
    }

    /// <summary>SC-006: one case per family and per detail group; every matching item, no item without that stem.</summary>
    [Theory]
    [InlineData("vocals", "vocals,vocals.lead,vocals.backing")]
    [InlineData("voice", "vocals,vocals.lead,vocals.backing")]
    [InlineData("lead vocals", "vocals.lead")]
    [InlineData("drums", "drums,drums.snare,drums.hihat")]
    [InlineData("snare", "drums.snare")]
    [InlineData("hi-hat", "drums.hihat")]
    [InlineData("bass", "bass")]
    [InlineData("bass guitar", "bass")]
    [InlineData("guitar", "guitar,guitar.acoustic-guitar")]
    [InlineData("acoustic", "guitar.acoustic-guitar")]
    [InlineData("piano", "piano,piano.keys,piano.synth")]
    [InlineData("keyboard", "piano,piano.keys,piano.synth")]
    [InlineData("synth", "piano.synth")]
    [InlineData("orchestra", "other,other.strings,other.brass")]
    [InlineData("strings", "other.strings")]
    [InlineData("brass", "other.brass")]
    public void EveryFamilyAndDetailGroup_FindsExactlyItsItems(string text, string expected)
    {
        var (snapshot, items) = InstrumentLibrary();

        ChannelsFound(snapshot, items, text).ShouldBe(expected.Split(','), ignoreOrder: true);
    }

    [Fact]
    public void Aliases_MatchOnlyAsAWholePhrase()
    {
        var (snapshot, items) = InstrumentLibrary();

        ChannelsFound(snapshot, items, "guitar").ShouldNotContain("bass");
        ChannelsFound(snapshot, items, "bass guitar").ShouldBe(["bass"]);
        ChannelsFound(snapshot, items, "guitar bass").ShouldBeEmpty();
        ChannelsFound(snapshot, items, "kit").ShouldBeEmpty();
        ChannelsFound(snapshot, items, "drum kit").ShouldBe(["drums", "drums.snare", "drums.hihat"], ignoreOrder: true);
    }

    [Fact]
    public void MatchedInstruments_AndInstrumentHighlights()
    {
        var (snapshot, items) = InstrumentLibrary();

        var hit = Search(snapshot, "snare").Items.Single(item => item.Asset.Id == items["drums.snare"].Id);

        hit.MatchedInstruments.ShouldBe(["Snare"]);
        hit.Highlights.ShouldContain(new HighlightDto { Field = SearchFieldDto.Instrument, Start = 0, Length = 5, Label = "Snare" });
    }

    [Fact]
    public void Folding_IgnoresCaseAndAccents_AndHighlightsTheOriginal()
    {
        var builder = new SnapshotBuilder();
        builder.Export(TrackA, Matched("Motörhead", "Ace of Spades", "Straße"));

        var artists = Search(builder.Build(), "MOTOR").Artists;
        var songs = Search(builder.Build(), "strasse").Songs;

        artists.Single().Highlights.Single().ShouldBe(new HighlightDto { Field = SearchFieldDto.Artist, Start = 0, Length = 5 });
        songs.Single().Highlights.Single().ShouldBe(new HighlightDto { Field = SearchFieldDto.Song, Start = 0, Length = 6 });
        songs.Single().Path.Select(parent => parent.Name).ShouldBe(["Motörhead", "Ace of Spades"]);
    }

    [Fact]
    public void Folders_NeedAWordInTheirOwnName()
    {
        var builder = new SnapshotBuilder();
        builder.Export(TrackA, Matched("Metallica", "Master of Puppets", "Battery"));

        var single = Search(builder.Build(), "metallica");
        var both = Search(builder.Build(), "metallica battery");

        (single.Totals.Artists, single.Totals.Albums, single.Totals.Songs, single.Totals.Items).ShouldBe((1, 0, 0, 1));
        (both.Totals.Artists, both.Totals.Albums, both.Totals.Songs, both.Totals.Items).ShouldBe((0, 0, 1, 1));
    }

    [Fact]
    public void EveryWord_MustMatchAField()
    {
        var builder = new SnapshotBuilder();
        builder.Export(TrackA, Matched("Metallica", "Master of Puppets", "Battery"), "bass");

        Search(builder.Build(), "metallica bass").Totals.Items.ShouldBe(1);
        Search(builder.Build(), "metallica piano").Totals.Items.ShouldBe(0);
        Search(builder.Build(), "zzzz").Totals.ShouldBe(new SearchTotalsDto { Artists = 0, Albums = 0, Songs = 0, Items = 0 });
    }

    [Fact]
    public void ArtistCredit_IsSearched()
    {
        var builder = new SnapshotBuilder();
        builder.Export(TrackA, Matched("Daft Punk", "RAM", "Get Lucky") with { Artist = "Daft Punk feat. Pharrell Williams" });

        Search(builder.Build(), "pharrell").Totals.Items.ShouldBe(1);
    }

    [Fact]
    public void Relevance_WholeThenWordStartThenInside()
    {
        var builder = new SnapshotBuilder();
        builder.Export(TrackA, Imported("Hibass", "x"));
        builder.Export(TrackA, Imported("Bassline Kings", "x"));
        builder.Export(TrackA, Imported("Bass", "x"));
        builder.Export(TrackA, Imported("The Bass Band", "x"));

        Search(builder.Build(), "bass").Artists.Select(hit => hit.Folder.Name).ShouldBe(["Bass", "The Bass Band", "Bassline Kings", "Hibass"]);
    }

    [Fact]
    public void Limit_CutsGroupsButNotTotals()
    {
        var builder = new SnapshotBuilder();
        for (var i = 0; i < 5; i++)
        {
            builder.Export(TrackA, Imported($"Band {i}", "Song"));
        }

        var result = Search(builder.Build(), "band", limit: 2);

        (result.Artists.Count, result.Totals.Artists, result.Items.Count, result.Totals.Items).ShouldBe((2, 5, 2, 5));
        (result.Query, result.Tokens.Single(), result.Revision).ShouldBe(("band", "band", Revision));
    }

    [Fact]
    public void Within_LimitsToTheFolder()
    {
        var builder = new SnapshotBuilder();
        var inside = builder.Export(TrackA, Matched("Metallica", "Master of Puppets", "Battery"));
        builder.Export(TrackA, Matched("Megadeth", "Rust in Peace", "Hangar 18"));

        var result = Search(builder.Build(), "a", inside.Placement!.ArtistFolderId);

        result.Artists.ShouldBeEmpty();
        result.Albums.Single().Folder.Name.ShouldBe("Master of Puppets");
        result.Items.Single().Asset.Id.ShouldBe(inside.Id);
        LibrarySearch.Run(builder.Build(), new SearchRequest("a", ["a"], Domain.Library.FolderId.Artist("nobody"), 20)).ShouldBeNull();
    }

    [Fact]
    public void ItemHit_HasItsPathAndAsset()
    {
        var builder = new SnapshotBuilder().Track(TrackA);
        var item = builder.Export(TrackA, Matched("Metallica", "Master of Puppets", "Battery"));

        var hit = Search(builder.Build(), "battery").Items.Single();

        hit.Path.Select(parent => parent.Level).ShouldBe([FolderLevelDto.Artist, FolderLevelDto.Album, FolderLevelDto.Song]);
        (hit.Asset.Id, hit.Asset.Provenance.TrackAvailable).ShouldBe((item.Id, true));
        hit.Highlights.ShouldContain(highlight => highlight.Field == SearchFieldDto.Song);
        hit.Highlights.ShouldContain(highlight => highlight.Field == SearchFieldDto.Name);
    }

    [Fact]
    public void Tokenize_FoldsSplitsAndCutsAtEight()
    {
        LibrarySearch.Tokenize("  The  BEATLES ").ShouldBe(["the", "beatles"]);
        LibrarySearch.Tokenize("a b c d e f g h i j").Count.ShouldBe(8);
        LibrarySearch.Tokenize("a a").ShouldBe(["a"]);
    }

    [Theory]
    [InlineData("metallica", "metallica", 0)]
    [InlineData("the metallica", "metallica", 1)]
    [InlineData("metallica", "tall", 2)]
    [InlineData("metallica", "zz", -1)]
    [InlineData("bassbass bass", "bass", 1)]
    public void ClassOf(string field, string token, int expected) => LibrarySearch.ClassOf(field, token).ShouldBe(expected);

    [Fact]
    public void Guards_RejectNull()
    {
        var snapshot = new SnapshotBuilder().Build();
        Should.Throw<ArgumentNullException>(() => LibrarySearch.Tokenize(null!));
        Should.Throw<ArgumentNullException>(() => LibrarySearch.Run(null!, new SearchRequest("a", ["a"], null, 1)));
        Should.Throw<ArgumentNullException>(() => LibrarySearch.Run(snapshot, null!));
        Should.Throw<ArgumentNullException>(() => LibrarySearch.ClassOf(null!, "a"));
        Should.Throw<ArgumentNullException>(() => LibrarySearch.ClassOf("a", null!));
        Should.Throw<ArgumentNullException>(() => LibrarySearch.Highlight(SearchFieldDto.Name, null!, "a"));
        Should.Throw<ArgumentNullException>(() => InstrumentMatcher.Match(null!, []));
        Should.Throw<ArgumentNullException>(() => InstrumentMatcher.Match([], null!));
        Should.Throw<ArgumentNullException>(() => SearchIndex.Build(null!));
    }
}
