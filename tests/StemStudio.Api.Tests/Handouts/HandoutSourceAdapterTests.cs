using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.Handouts.Sources;
using StemStudio.Api.Tests.InstrumentViews;
using StemStudio.Api.Tests.Setlists;
using StemStudio.Api.Tests.SongSections;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.Practice;
using StemStudio.Domain.Assets;
using StemStudio.Domain.Handouts;
using StemStudio.Domain.InstrumentViews;
using StemStudio.Domain.Scores;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Tests.Handouts;

/// <summary>
/// Feature 020 T024: the source ports over the merged 014/016 services (DELTA "Phase 0 outcome" P0-4): sections with 014's
/// start bar, setlists (<c>note</c> "" -> null, 1-based positions), best trainer tempo per section, the 016 chord chart and
/// notation items classified by their score facts.
/// </summary>
public sealed class HandoutSourceAdapterTests : ApiTestBase
{
    private static readonly DateTime T0 = SyncTestData.T0;

    [Fact]
    public async Task Sections_AreInOrder_WithTheStartBarOfTheReliableGrid()
    {
        var track = await Factory.SeedAsync(GridTrack(reliable: true));
        await Factory.SeedDetectedAsync(track.Id);

        var sections = await WithSourceAsync<ISectionSource, IReadOnlyList<SongSection>>(source => source.SectionsAsync(track, Ct));

        sections.Count.ShouldBe(5);
        sections.Select(section => section.StartSeconds).ShouldBeInOrder();
        (sections[0].Label, sections[0].StartSeconds, sections[0].StartBar).ShouldBe(("Intro", 0d, (int?)0));
        sections[1].StartBar.ShouldBe(4, "downbeats every 2 s from 2 s: 8.1 s is in bar 4");
        sections.ShouldAllBe(section => section.Id != Guid.Empty);
    }

    [Fact]
    public async Task Sections_HaveNoStartBarWithoutAReliableGrid_AndATrackWithoutSectionsHasNone()
    {
        var unreliable = await Factory.SeedAsync(GridTrack(reliable: false));
        var empty = await Factory.SeedAsync(GridTrack(reliable: true, videoId: "aaaaaaaaaaa"));
        await Factory.SeedDetectedAsync(unreliable.Id);

        var withoutBars = await WithSourceAsync<ISectionSource, IReadOnlyList<SongSection>>(source => source.SectionsAsync(unreliable, Ct));
        var none = await WithSourceAsync<ISectionSource, IReadOnlyList<SongSection>>(source => source.SectionsAsync(empty, Ct));

        withoutBars.ShouldAllBe(section => section.StartBar == null);
        none.ShouldBeEmpty();
        await Should.ThrowAsync<ArgumentNullException>(() => WithSourceAsync<ISectionSource, IReadOnlyList<SongSection>>(source => source.SectionsAsync(null!, Ct)));
    }

    [Fact]
    public async Task Setlist_MapsEmptyNotesToNull_AndPositionsFromOne()
    {
        var (first, second) = (await Factory.SeedAsync(SyncTestData.CompletedTrack("aaaaaaaaaaa")), await Factory.SeedAsync(SyncTestData.CompletedTrack("bbbbbbbbbbb")));
        var created = await Client.CreateSetlistAsync("Band night", note: string.Empty);
        (await Client.PutEntriesAsync(created.Id, SetlistTestData.Entry(second.Id, note: "Capo 2"), SetlistTestData.Entry(first.Id))).EnsureSuccessStatusCode();
        var noted = await Client.CreateSetlistAsync("Rehearsal", note: "Bring the capo.");

        var setlist = (await WithSourceAsync<ISetlistSource, SetlistSnapshot?>(source => source.FindAsync(created.Id, Ct)))!;
        var withNote = (await WithSourceAsync<ISetlistSource, SetlistSnapshot?>(source => source.FindAsync(noted.Id, Ct)))!;

        (setlist.Id, setlist.Name, setlist.Notes).ShouldBe((created.Id, "Band night", (string?)null));
        setlist.Entries.ShouldBe([new SetlistSong(1, second.Id, "Capo 2"), new SetlistSong(2, first.Id, null)]);
        (withNote.Notes, withNote.Entries.Count).ShouldBe(("Bring the capo.", 0));
        (await WithSourceAsync<ISetlistSource, SetlistSnapshot?>(source => source.FindAsync(Guid.NewGuid(), Ct))).ShouldBeNull();
    }

    [Fact]
    public async Task PracticeLog_GivesTheBestTrainerTempoPerSection_Rounded()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());
        var sections = await Factory.SeedDetectedAsync(track.Id);
        var (verse, chorus) = (sections.Sections[1].Id, sections.Sections[2].Id);
        await PutSessionAsync(track.Id, T0.AddHours(-3), Section(verse, "Verse", 72.4), Section(chorus, "Chorus", null), Section(null, "Free", 90));
        await PutSessionAsync(track.Id, T0.AddHours(-2), Section(verse, "Verse", 84.5));

        var best = await WithSourceAsync<IPracticeLogSource, IReadOnlyDictionary<Guid, int>>(source => source.BestTempoAsync(track.Id, Ct));
        var none = await WithSourceAsync<IPracticeLogSource, IReadOnlyDictionary<Guid, int>>(source => source.BestTempoAsync(Guid.NewGuid(), Ct));

        best.ShouldBe(new Dictionary<Guid, int> { [verse] = 85 });
        none.ShouldBeEmpty();
    }

    [Fact]
    public async Task Chords_AreTheReadyOrStaleChart_ElseNull()
    {
        var (ready, queued, none) = (await Factory.SeedAsync(SyncTestData.CompletedTrack("aaaaaaaaaaa")), await Factory.SeedAsync(SyncTestData.CompletedTrack("bbbbbbbbbbb")), await Factory.SeedAsync(SyncTestData.CompletedTrack("ccccccccccc")));
        await Factory.SeedReadyAsync(ready.Id, AnalysisTestData.Chart(), new string('f', 64));
        await Factory.SeedQueuedAsync(queued.Id, AnalysisKind.Chords, AnalysisTestData.JobId);

        var chart = await WithSourceAsync<IChordSource, ChordChart?>(source => source.ChartAsync(ready.Id, Ct));

        chart!.Segments.Count.ShouldBe(3);
        chart.Key!.Key.ShouldBe(new MusicalKey(0, KeyMode.Major));
        (await WithSourceAsync<IChordSource, ChordChart?>(source => source.ChartAsync(queued.Id, Ct))).ShouldBeNull();
        (await WithSourceAsync<IChordSource, ChordChart?>(source => source.ChartAsync(none.Id, Ct))).ShouldBeNull();
    }

    [Fact]
    public async Task Notation_ClassifiesReadyScoresByTheirFacts_NewestFirst()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());
        var other = await Factory.SeedAsync(SyncTestData.CompletedTrack("zzzzzzzzzzz"));
        var bassScore = Score(track.Id, "bass", ScoreProfile.Bass, 1);
        var bassTab = Score(track.Id, "bass", ScoreProfile.Bass, 2, facts => facts with { Tablature = new TablatureFacts { Tuning = TuningId.Bass4Standard, Strings = 4, Capo = 0, Layout = TabLayout.NotationAndTab } });
        var drums = Score(track.Id, "drums", ScoreProfile.Drums, 3);
        var trumpet = Score(track.Id, "other", ScoreProfile.Brass, 4, facts => facts with { Part = Part(WrittenInstrument.TrumpetBb) });
        var flute = Score(track.Id, "other", ScoreProfile.Woodwinds, 5, facts => facts with { Part = Part(WrittenInstrument.Flute) });
        var horn = Score(track.Id, "other", ScoreProfile.Brass, 6, facts => facts with { Part = Part(WrittenInstrument.HornF) });
        var alto = Score(track.Id, "other", ScoreProfile.Woodwinds, 7, facts => facts with { Part = Part(WrittenInstrument.AltoSaxEb), Attribution = "Model: x" });
        var failed = QueuedScore(track.Id, 8);
        failed.Fail("broken", T0);
        await SeedAssetsAsync(bassScore, bassTab, drums, trumpet, flute, horn, alto, failed, QueuedScore(track.Id, 9), Score(other.Id, "bass", ScoreProfile.Bass, 10));

        var items = await WithSourceAsync<INotationCatalog, IReadOnlyList<NotationItem>>(source => source.ItemsAsync(track.Id, Ct));

        items.Select(item => item.AssetId).ShouldBe([alto.Id, horn.Id, flute.Id, trumpet.Id, drums.Id, bassTab.Id, bassScore.Id]);
        items.Select(item => (item.Kind, item.Transposition)).ShouldBe([
            (NotationKind.Transposed, Transposition.EFlat), (NotationKind.Transposed, Transposition.F), (NotationKind.Score, Transposition.C),
            (NotationKind.Transposed, Transposition.BFlat), (NotationKind.Drums, null), (NotationKind.Tab, null), (NotationKind.Score, null),
        ]);
        var first = items[0];
        (first.PdfPath, first.Channel, first.Profile, first.TrackId, first.Attribution, first.Title).ShouldBe(($"assets/{alto.Id}/score.pdf", "other", ScoreProfile.Woodwinds, track.Id, "Model: x", "Song 7"));
        items[^1].Key!.Tonic.ShouldBe("A");
    }

    [Theory]
    [InlineData(WrittenInstrument.Concert, Transposition.C)]
    [InlineData(WrittenInstrument.ClarinetBb, Transposition.BFlat)]
    [InlineData(WrittenInstrument.TenorSaxBb, Transposition.BFlat)]
    [InlineData(WrittenInstrument.BaritoneSaxEb, Transposition.EFlat)]
    [InlineData(WrittenInstrument.EnglishHornF, Transposition.F)]
    [InlineData(WrittenInstrument.Violin, Transposition.C)]
    public void WrittenTransposition_FollowsTheInstrumentName(WrittenInstrument instrument, Transposition expected) =>
        NotationClassifier.Written(instrument).ShouldBe(expected);

    [Fact]
    public void Classify_RefusesNull() => Should.Throw<ArgumentNullException>(() => NotationClassifier.Classify(null!));

    private static Track GridTrack(bool reliable, string videoId = "dQw4w9WgXcQ")
    {
        var track = SyncTestData.UrlTrack(videoId);
        var downbeats = Enumerable.Range(1, 40).Select(bar => bar * 2.0).ToList();
        track.Complete(new TrackCompletion(90, "htdemucs_6s", StemCatalog.SixStem.Select(name => new Stem(name, 10)).ToList(), new BeatGrid { Bpm = 120, Beats = downbeats, Downbeats = downbeats, BeatsPerBar = 4, Confidence = 0.9, Reliable = reliable }), T0);
        return track;
    }

    private static PracticeSectionInputDto Section(Guid? id, string label, double? trainer) =>
        new() { SectionId = id, Kind = PracticeSectionKindDto.Verse, Label = label, PlayedSeconds = 30, TrainerReachedPercent = trainer };

    private async Task PutSessionAsync(Guid trackId, DateTime started, params PracticeSectionInputDto[] sections)
    {
        var session = new PracticeSessionInputDto { TrackId = trackId, StartedAt = started, EndedAt = started.AddMinutes(5), PlayedSeconds = 120, Sections = sections };
        using var response = await Client.PutAsJsonAsync(ApiRoutes.PracticeSession(Guid.NewGuid()), session, Ct);
        response.EnsureSuccessStatusCode();
    }

    private static PartFacts Part(WrittenInstrument instrument) => new() { Instrument = instrument, Name = instrument.ToString(), Interval = "P1", Clef = ClefChoice.Treble };

    private static Asset QueuedScore(Guid trackId, int minute)
    {
        var provenance = new Provenance
        {
            Origin = AssetOrigin.Transcription,
            TrackId = trackId,
            TrackTitle = "Song",
            Contents = "Sheet music",
            ScoreRecipe = new ScoreRecipe { Source = ScoreSource.FromChannel(trackId, "bass"), Profile = ScoreProfile.Bass, Resolution = ScoreResolution.Sixteenth },
        };
        return Asset.CreateScore(Guid.NewGuid(), $"Song {minute}", provenance, T0.AddMinutes(minute));
    }

    private static Asset Score(Guid trackId, string channel, ScoreProfile profile, int minute, Func<ScoreFacts, ScoreFacts>? change = null)
    {
        var provenance = new Provenance
        {
            Origin = AssetOrigin.Transcription,
            TrackId = trackId,
            TrackTitle = "Song",
            Contents = "Sheet music",
            ScoreRecipe = new ScoreRecipe { Source = ScoreSource.FromChannel(trackId, channel), Profile = profile, Resolution = ScoreResolution.Sixteenth },
        };
        var asset = Asset.CreateScore(Guid.NewGuid(), $"Song {minute}", provenance, T0.AddMinutes(minute));
        asset.AssignJob("c4e6a8b0d2f4416385a7c9e1b3d5f7a9", T0);
        var facts = new ScoreFacts
        {
            Profile = profile,
            Engine = ScoreEngine.BasicPitch,
            Key = new KeyInfo("A", KeyMode.Minor, 0.8, KeySource.Estimated),
            Bpm = 120,
            TimeSignature = "4/4",
            Bars = 10,
            Notes = 40,
            Pages = 1,
            Resolution = ScoreResolution.Sixteenth,
        };
        asset.CompleteScore([new AssetFile("pdf", "score.pdf", "application/pdf", 100, new string('a', 64))], change?.Invoke(facts) ?? facts, T0.AddMinutes(minute));
        return asset;
    }

    private Task<bool> SeedAssetsAsync(params Asset[] items) => Factory.WithScopeAsync(async sp =>
    {
        var assets = sp.GetRequiredService<IAssetRepository>();
        foreach (var item in items)
        {
            assets.Add(item);
        }

        await assets.SaveAsync(CancellationToken.None);
        return true;
    });

    private Task<TResult> WithSourceAsync<TSource, TResult>(Func<TSource, Task<TResult>> read)
        where TSource : notnull =>
        Factory.WithScopeAsync(sp => read(sp.GetRequiredService<TSource>()));
}
