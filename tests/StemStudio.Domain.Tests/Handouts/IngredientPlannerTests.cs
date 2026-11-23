using StemStudio.Domain.Handouts;
using StemStudio.Domain.Lyrics;
using StemStudio.Domain.Scores;
using StemStudio.Domain.Tracks;
using static StemStudio.Domain.Tests.InstrumentViews.ChartData;

namespace StemStudio.Domain.Tests.Handouts;

/// <summary>
/// Feature 020 T060 (and the pure parts of T062): families and their defaults (data-model 6.2), omissions with reasons, 422 for
/// codes a family does not offer, notation per family (6.1), availability, attributions and names.
/// </summary>
public sealed class IngredientPlannerTests
{
    private static readonly DateTime T0 = new(2026, 11, 23, 12, 0, 0, DateTimeKind.Utc);

    public static TheoryData<HandoutFamily, IngredientCode[]> FamilyDefaults => new()
    {
        { HandoutFamily.Overview, [IngredientCode.Facts, IngredientCode.FormChart, IngredientCode.ChordsOverLyrics, IngredientCode.ChordChart, IngredientCode.PracticeNotes] },
        { HandoutFamily.Vocals, [IngredientCode.Facts, IngredientCode.FormChart, IngredientCode.ChordsOverLyrics, IngredientCode.Notation, IngredientCode.PracticeNotes] },
        { HandoutFamily.Guitar, [IngredientCode.Facts, IngredientCode.FormChart, IngredientCode.ChordsOverLyrics, IngredientCode.ChordChart, IngredientCode.Notation, IngredientCode.PracticeNotes] },
        { HandoutFamily.Bass, [IngredientCode.Facts, IngredientCode.FormChart, IngredientCode.ChordChart, IngredientCode.Notation, IngredientCode.PracticeNotes] },
        { HandoutFamily.Keys, [IngredientCode.Facts, IngredientCode.FormChart, IngredientCode.ChordsOverLyrics, IngredientCode.ChordChart, IngredientCode.Notation, IngredientCode.PracticeNotes] },
        { HandoutFamily.Drums, [IngredientCode.Facts, IngredientCode.FormChart, IngredientCode.Notation, IngredientCode.PracticeNotes] },
        { HandoutFamily.Melodic, [IngredientCode.Facts, IngredientCode.FormChart, IngredientCode.ChordChart, IngredientCode.Notation, IngredientCode.PracticeNotes] },
    };

    [Theory]
    [MemberData(nameof(FamilyDefaults))]
    public void EveryFamily_HasItsDefaults_AndFactsFormAndNotes(HandoutFamily family, IngredientCode[] defaults)
    {
        IngredientPlanner.Defaults(family, HandoutKind.Song).ShouldBe(defaults);
        IngredientPlanner.Defaults(family, HandoutKind.Setlist).ShouldBe([.. defaults, IngredientCode.SetlistNotes]);
        Families.Offered(family, HandoutKind.Song).ShouldContain(IngredientCode.PracticeProgress);
        Families.Offered(family, HandoutKind.Song).ShouldNotContain(IngredientCode.SetlistNotes);
    }

    [Fact]
    public void CodesNotOfferedToAFamily_AreRefused()
    {
        IngredientPlanner.Refused(HandoutFamily.Drums, HandoutKind.Song, [IngredientCode.ChordsOverLyrics, IngredientCode.ChordChart, IngredientCode.Facts])
            .ShouldBe(["chords-over-lyrics is not offered for the drums family.", "chord-chart is not offered for the drums family."]);
        IngredientPlanner.Refused(HandoutFamily.Overview, HandoutKind.Song, [IngredientCode.Notation, IngredientCode.SetlistNotes]).Count.ShouldBe(2);
        IngredientPlanner.Refused(HandoutFamily.Overview, HandoutKind.Setlist, [IngredientCode.SetlistNotes]).ShouldBeEmpty();
        IngredientPlanner.Refused(HandoutFamily.Bass, HandoutKind.Song, null).ShouldBeEmpty();
    }

    [Fact]
    public void ThePlan_IncludesWhatIsAvailable_AndListsTheRestWithReasons()
    {
        var availability = new Dictionary<IngredientCode, IngredientAvailability>
        {
            [IngredientCode.Facts] = IngredientAvailability.Available,
            [IngredientCode.FormChart] = IngredientAvailability.Available,
            [IngredientCode.ChordChart] = IngredientAvailability.NoTiming,
            [IngredientCode.PracticeNotes] = IngredientAvailability.Available,
        };

        var plan = IngredientPlanner.Plan(HandoutFamily.Bass, HandoutKind.Song, null, availability);

        plan.Included.ShouldBe([IngredientCode.Facts, IngredientCode.FormChart, IngredientCode.PracticeNotes]);
        plan.Omitted.ShouldBe([
            new OmittedIngredient(IngredientCode.ChordsOverLyrics, OmissionReason.NotSelected),
            new OmittedIngredient(IngredientCode.Lyrics, OmissionReason.NotSelected),
            new OmittedIngredient(IngredientCode.ChordChart, OmissionReason.NoTiming),
            new OmittedIngredient(IngredientCode.Notation, OmissionReason.NotAvailable),
            new OmittedIngredient(IngredientCode.PracticeProgress, OmissionReason.NotSelected),
        ]);
    }

    [Fact]
    public void ARequestedSelection_ReplacesTheDefaults()
    {
        var all = Enum.GetValues<IngredientCode>().ToDictionary(code => code, _ => IngredientAvailability.Available);

        var plan = IngredientPlanner.Plan(HandoutFamily.Melodic, HandoutKind.Song, [IngredientCode.Facts, IngredientCode.Lyrics], all);

        plan.Included.ShouldBe([IngredientCode.Facts, IngredientCode.Lyrics]);
        plan.Omitted.ShouldAllBe(item => item.Reason == OmissionReason.NotSelected);
        Should.Throw<ArgumentNullException>(() => IngredientPlanner.Plan(HandoutFamily.Bass, HandoutKind.Song, null, null!));
    }

    [Theory]
    [InlineData(HandoutFamily.Overview, null, "Band overview")]
    [InlineData(HandoutFamily.Keys, null, "Keys")]
    [InlineData(HandoutFamily.Melodic, Transposition.C, "Melodic instruments")]
    [InlineData(HandoutFamily.Melodic, Transposition.BFlat, "Melodic instruments in B-flat")]
    [InlineData(HandoutFamily.Melodic, Transposition.EFlat, "Melodic instruments in E-flat")]
    [InlineData(HandoutFamily.Melodic, Transposition.F, "Melodic instruments in F")]
    public void Labels(HandoutFamily family, Transposition? transposition, string label) => Families.Label(family, transposition).ShouldBe(label);

    [Fact]
    public void Notation_FitsItsFamily_AndDefaultsToTheNewestPerKind()
    {
        var items = new[]
        {
            Item(ScoreProfile.Bass, NotationKind.Tab, null), Item(ScoreProfile.Bass, NotationKind.Score, null), Item(ScoreProfile.Bass, NotationKind.Score, null),
            Item(ScoreProfile.Guitar, NotationKind.Score, null), Item(ScoreProfile.Piano, NotationKind.Score, null), Item(ScoreProfile.Keys, NotationKind.Score, null),
            Item(ScoreProfile.Drums, NotationKind.Drums, null), Item(ScoreProfile.Vocals, NotationKind.Score, null),
            Item(ScoreProfile.Brass, NotationKind.Transposed, Transposition.BFlat), Item(ScoreProfile.Woodwinds, NotationKind.Score, Transposition.C),
            Item(ScoreProfile.Woodwinds, NotationKind.Transposed, Transposition.EFlat), Item(ScoreProfile.Strings, NotationKind.Score, null),
        };

        Families.DefaultNotation(HandoutFamily.Bass, items, Transposition.C).ShouldBe([items[0], items[1]]);
        Families.DefaultNotation(HandoutFamily.Guitar, items, Transposition.C).ShouldBe([items[3]]);
        Families.DefaultNotation(HandoutFamily.Keys, items, Transposition.C).ShouldBe([items[4]]);
        Families.DefaultNotation(HandoutFamily.Drums, items, Transposition.C).ShouldBe([items[6]]);
        Families.DefaultNotation(HandoutFamily.Vocals, items, Transposition.C).ShouldBe([items[7]]);
        Families.DefaultNotation(HandoutFamily.Melodic, items, Transposition.BFlat).ShouldBe([items[8]]);
        Families.DefaultNotation(HandoutFamily.Melodic, items, Transposition.EFlat).ShouldBe([items[10]]);
        Families.DefaultNotation(HandoutFamily.Melodic, items, Transposition.F).ShouldBeEmpty();
        Families.DefaultNotation(HandoutFamily.Melodic, items, Transposition.C).ShouldBe([items[9]]);
        items.Where(item => Families.Fits(HandoutFamily.Melodic, item, Transposition.C)).Count().ShouldBe(2);
        Families.DefaultNotation(HandoutFamily.Overview, items, Transposition.C).ShouldBeEmpty();
        Families.Fits(HandoutFamily.Melodic, Item(ScoreProfile.Bass, NotationKind.Score, null) with { Channel = "other.flute" }, Transposition.C).ShouldBeTrue();
        Should.Throw<ArgumentNullException>(() => Families.DefaultNotation(HandoutFamily.Bass, null!, Transposition.C));
        Should.Throw<ArgumentNullException>(() => Families.Fits(HandoutFamily.Bass, null!, Transposition.C));
    }

    [Fact]
    public void Availability_FollowsTheSongsData()
    {
        var empty = Material();
        var full = Material() with
        {
            Grid = Grid(8),
            Sections = [new SongSection(Guid.Empty, "Verse", 0, 8, null)],
            Chart = Chart(("C:maj", 2000)),
            Lyrics = LyricsOf(SyncLevel.Word, Line(0, 1000, ("Hi", 0))),
            BestTempo = new Dictionary<Guid, int> { [Guid.Empty] = 80 },
            PracticeNote = "Note",
            EntryNotes = "Capo",
        };

        HandoutAvailability.Of(empty, []).Where(pair => pair.Value == IngredientAvailability.Available).Select(pair => pair.Key).ShouldBe([IngredientCode.Facts]);
        HandoutAvailability.Of(full, [Item(ScoreProfile.Bass, NotationKind.Score, null)]).Values.ShouldAllBe(value => value == IngredientAvailability.Available);
        var untimed = HandoutAvailability.Of(full with { Grid = null, Lyrics = full.Lyrics! with { SyncLevel = SyncLevel.Plain } }, []);
        (untimed[IngredientCode.ChordChart], untimed[IngredientCode.ChordsOverLyrics], untimed[IngredientCode.Lyrics]).ShouldBe((IngredientAvailability.NoTiming, IngredientAvailability.NoTiming, IngredientAvailability.Available));
        HandoutAvailability.Of(full with { Lyrics = full.Lyrics! with { Instrumental = true } }, [])[IngredientCode.Lyrics].ShouldBe(IngredientAvailability.Missing);
        HandoutAvailability.Of(full with { Sections = [] }, [])[IngredientCode.ChordChart].ShouldBe(IngredientAvailability.Missing);
    }

    [Fact]
    public void Combine_TakesTheBestSong_AndTheSetlistNotes()
    {
        var a = HandoutAvailability.Of(Material(), []);
        var b = HandoutAvailability.Of(Material() with { PracticeNote = "x" }, []);

        var combined = HandoutAvailability.Combine([a, b], setlistNotes: true);

        (combined[IngredientCode.PracticeNotes], combined[IngredientCode.SetlistNotes], combined[IngredientCode.Lyrics]).ShouldBe((IngredientAvailability.Available, IngredientAvailability.Available, IngredientAvailability.Missing));
        HandoutAvailability.Combine([], false)[IngredientCode.Facts].ShouldBe(IngredientAvailability.Missing);
    }

    [Fact]
    public void Attributions_OnlyThoseThatApply_DistinctAndCut()
    {
        var lyrics = LyricsOf(SyncLevel.Word, Line(0, 1000, ("Hi", 0)));
        var piano = Item(ScoreProfile.Piano, NotationKind.Score, null) with { Attribution = "Piano model (CC BY 4.0)" };
        var long_ = Item(ScoreProfile.Piano, NotationKind.Score, null) with { Attribution = new string('a', 400) };

        var lines = HandoutNotice.Attributions([
            new AttributionSources(lyrics, true, [piano], new TrackArtwork(ArtworkSource.CoverArtArchive, new string('a', 64), T0)),
            new AttributionSources(lyrics, true, [piano, long_], null),
            new AttributionSources(null, false, [], null),
        ]);

        lines.ShouldBe(["Lyrics: LRCLIB", HandoutNotice.ChordsLine, "Piano model (CC BY 4.0)", "Artwork: Cover Art Archive", new string('a', 300)]);
        HandoutNotice.ArtworkLine(new TrackArtwork(ArtworkSource.User, new string('a', 64), T0)).ShouldBe("Artwork: your own picture");
        HandoutNotice.ArtworkLine(new TrackArtwork(ArtworkSource.Spotify, new string('a', 64), T0)).ShouldBe("Artwork: Spotify");
        HandoutNotice.ArtworkLine(new TrackArtwork(ArtworkSource.Embedded, new string('a', 64), T0)).ShouldBe("Artwork: embedded in the audio file");
        Should.Throw<ArgumentNullException>(() => HandoutNotice.Attributions(null!));
        Should.Throw<ArgumentNullException>(() => HandoutNotice.ArtworkLine(null!));
    }

    [Fact]
    public void Naming_AndWireValues()
    {
        HandoutNaming.Contents("Bass").ShouldBe("Bass handout");
        Should.Throw<ArgumentException>(() => HandoutNaming.Contents(" "));
        IngredientPlanner.Wire(Transposition.BFlat).ShouldBe("b-flat");
        IngredientPlanner.Wire(IngredientCode.PracticeProgress).ShouldBe("practice-progress");
        Families.TranspositionName(Transposition.C).ShouldBe("C");
    }

    private static HandoutMaterial Material() => new() { TrackId = Guid.NewGuid(), Title = "Song" };

    private static NotationItem Item(ScoreProfile profile, NotationKind kind, Transposition? transposition) => new()
    {
        AssetId = Guid.NewGuid(),
        Title = $"{profile} {kind}",
        TrackId = Guid.Empty,
        Profile = profile,
        Kind = kind,
        Transposition = transposition,
        PdfPath = "assets/x/score.pdf",
        CreatedAt = T0,
    };
}
