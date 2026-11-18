using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StemStudio.Domain.Assets;
using StemStudio.Domain.InstrumentViews;
using StemStudio.Domain.Scores;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence.Configurations;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Persistence.Tests;

/// <summary>Feature 016 T047: the <c>TrackAnalyses</c> table (data-model 7.1, migration AddTrackAnalyses) and the score JSON additions.</summary>
public sealed class TrackAnalysesPersistenceTests
{
    private const string LastMigrationBefore016 = "20261117215712_AddRecordingTakes";
    private const string JobId = "4f1c2b3a4d5e6f708192a3b4c5d6e7f8";
    private static readonly DateTime T0 = TestData.T0;
    private static readonly AnalysisRun Run = new(JobId, new AnalysisOptions(ChordVocabulary.Full, "chroma-hmm"), new string('f', 64), 20);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Migration_AppliesOnAPre016Database()
    {
        var path = Path.Combine(Path.GetTempPath(), $"stemstudio-016-{Guid.NewGuid():N}.db");
        try
        {
            var connectionString = $"Data Source={path};Pooling=False";
            await SqliteTestDatabase.MigrateLegacyAsync(connectionString, LastMigrationBefore016);
            await using var db = await SqliteTestDatabase.CreateAsync(connectionString);
            await using var read = db.NewContext();

            read.Database.GetPendingMigrations().ShouldBeEmpty();
            read.Database.GetAppliedMigrations().ShouldContain(id => id.EndsWith("_AddTrackAnalyses", StringComparison.Ordinal));
            (await db.ScalarAsync("SELECT COUNT(*) FROM TrackAnalyses")).ShouldBe("0");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task AChordChart_RoundTripsWithKeyRegionsEditsAndOptions()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var track = await SeedTrackAsync(db);
        var analysis = ReadyChords(track.Id);
        analysis.EditChord(1, "A:min7/b7", T0.AddMinutes(2));
        await using (var write = db.NewContext())
        {
            await new TrackAnalysisRepository(write).AddAsync(analysis, Ct);
        }

        await using var read = db.NewContext();
        var loaded = (await new TrackAnalysisRepository(read).FindAsync(track.Id, AnalysisKind.Chords, Ct))!;

        (loaded.Status, loaded.Engine, loaded.Progress, loaded.SourceFingerprint, loaded.UpdatedAt).ShouldBe((AnalysisStatus.Ready, "btc", 1d, new string('f', 64), T0.AddMinutes(2)));
        loaded.Options.ShouldBe(new AnalysisOptions(ChordVocabulary.Full, "chroma-hmm"));
        loaded.Device.ShouldBe(new JobDevice(ProcessingDeviceKind.Gpu, "RTX", false, null));
        var chart = loaded.Document.ShouldBeOfType<ChordChart>();
        chart.Key.ShouldBe(new SongKey(0, KeyMode.Major, 0.81, new MusicalKey(9, KeyMode.Minor)));
        chart.KeyRegions.ShouldBe([new KeyRegion(500, 16500, 0, KeyMode.Major, 0.84), new KeyRegion(16500, 32500, 2, KeyMode.Major, 0.77)]);
        chart.Segments[1].ShouldBe(new ChordSegment { StartMs = 500, EndMs = 2500, Label = "A:min7/b7", Root = 9, Quality = ChordQuality.Min7, Bass = 7, Confidence = 0.9, Edited = true });
        (chart.HasEdits, chart.PreferFlats, chart.BeatSnapped, chart.Vocabulary).ShouldBe((true, false, true, ChordVocabulary.Simple));
        chart.Warnings.ShouldBe([new AnalysisWarning(AnalysisWarningCode.KeyUncertain, "The key is uncertain (confidence 45 %).")]);
        (await db.ScalarAsync("SELECT Kind || '/' || Status FROM TrackAnalyses")).ShouldBe("Chords/Ready");
        (await db.ScalarAsync("SELECT json_extract(DocumentJson, '$.\"$type\"') FROM TrackAnalyses")).ShouldBe("chords");
        (await db.ScalarAsync("SELECT instr(DocumentJson, 'HasEdits') FROM TrackAnalyses")).ShouldBe("0", "computed properties are not stored");
    }

    [Fact]
    public async Task AVocalPitchDocumentOf90000Frames_RoundTrips()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var track = await SeedTrackAsync(db);
        var frames = Enumerable.Range(0, 90_000).Select(index => index % 7 == 0 ? 0 : 5700 + (index % 1200)).ToArray();
        var analysis = Queued(track.Id, AnalysisKind.VocalPitch);
        analysis.Complete(new VocalPitchDocument { Engine = "pyin", Frames = frames, Source = VocalSource.VocalsLead, VoicedRatio = 0.86, Range = new PitchRange(5701, 6899) }, null, T0);
        await using (var write = db.NewContext())
        {
            await new TrackAnalysisRepository(write).AddAsync(analysis, Ct);
        }

        await using var read = db.NewContext();
        var pitch = (await new TrackAnalysisRepository(read).FindAsync(track.Id, AnalysisKind.VocalPitch, Ct))!.Document.ShouldBeOfType<VocalPitchDocument>();

        pitch.Frames.ShouldBe(frames);
        (pitch.HopMs, pitch.Source, pitch.VoicedRatio, pitch.Range, pitch.Engine).ShouldBe((10, VocalSource.VocalsLead, 0.86, new PitchRange(5701, 6899), "pyin"));
    }

    [Fact]
    public async Task DrumHits_RoundTripAndChangesAreSaved()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var track = await SeedTrackAsync(db);
        await using (var write = db.NewContext())
        {
            await new TrackAnalysisRepository(write).AddAsync(Queued(track.Id, AnalysisKind.DrumHits), Ct);
        }

        await using (var update = db.NewContext())
        {
            var repository = new TrackAnalysisRepository(update);
            var stored = (await repository.FindAsync(track.Id, AnalysisKind.DrumHits, Ct))!;
            stored.Complete(new DrumHitsDocument { Engine = "drum-onsets", Pieces = [DrumPiece.Kick, DrumPiece.Snare], Hits = [new DrumHit(10, DrumPiece.Kick, 0.9), new DrumHit(510, DrumPiece.Snare, 0.5)] }, null, T0.AddMinutes(1));
            await repository.SaveAsync(Ct);
        }

        await using var read = db.NewContext();
        var hits = (await new TrackAnalysisRepository(read).FindAsync(track.Id, AnalysisKind.DrumHits, Ct))!.Document.ShouldBeOfType<DrumHitsDocument>();
        hits.Hits.ShouldBe([new DrumHit(10, DrumPiece.Kick, 0.9), new DrumHit(510, DrumPiece.Snare, 0.5)]);
        hits.Pieces.ShouldBe([DrumPiece.Kick, DrumPiece.Snare]);
    }

    [Fact]
    public async Task TrackAndKind_AreUnique()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var track = await SeedTrackAsync(db);
        await using (var write = db.NewContext())
        {
            await new TrackAnalysisRepository(write).AddAsync(TrackAnalysis.Create(track.Id, AnalysisKind.Chords, T0), Ct);
            await new TrackAnalysisRepository(write).AddAsync(TrackAnalysis.Create(track.Id, AnalysisKind.DrumHits, T0), Ct);
        }

        await using var duplicate = db.NewContext();
        await Should.ThrowAsync<Exception>(() => new TrackAnalysisRepository(duplicate).AddAsync(TrackAnalysis.Create(track.Id, AnalysisKind.Chords, T0), Ct));
    }

    [Fact]
    public async Task Analyses_AreDeletedWithTheirTrack()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var track = await SeedTrackAsync(db);
        await using (var write = db.NewContext())
        {
            await new TrackAnalysisRepository(write).AddAsync(ReadyChords(track.Id), Ct);
        }

        await using (var delete = db.NewContext())
        {
            delete.Tracks.Remove((await delete.Tracks.FindAsync([track.Id], Ct))!);
            await delete.SaveChangesAsync(Ct);
        }

        (await db.ScalarAsync("SELECT COUNT(*) FROM TrackAnalyses")).ShouldBe("0");
    }

    [Fact]
    public async Task ActiveStatusesAndReadyChords_AreQueried()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var (first, second) = (await SeedTrackAsync(db), await SeedTrackAsync(db, "aaaaaaaaaaa"));
        await using (var write = db.NewContext())
        {
            var repository = new TrackAnalysisRepository(write);
            await repository.AddAsync(ReadyChords(first.Id), Ct);
            await repository.AddAsync(Queued(first.Id, AnalysisKind.DrumHits), Ct);
            await repository.AddAsync(Queued(second.Id, AnalysisKind.Chords), Ct);
        }

        await using var read = db.NewContext();
        var analyses = new TrackAnalysisRepository(read);
        (await analyses.ActiveAsync(Ct)).Select(a => (a.TrackId, a.Kind)).ShouldBe([(first.Id, AnalysisKind.DrumHits), (second.Id, AnalysisKind.Chords)], ignoreOrder: true);
        (await analyses.StatusesAsync([first.Id], Ct)).ShouldBe(
            [new AnalysisStatusRow(first.Id, AnalysisKind.Chords, AnalysisStatus.Ready), new AnalysisStatusRow(first.Id, AnalysisKind.DrumHits, AnalysisStatus.Queued)], ignoreOrder: true);
        (await analyses.ReadyChordsAsync([first.Id, second.Id], Ct)).Keys.ShouldBe([first.Id]);
    }

    [Fact]
    public async Task UnreadableDocumentText_LoadsAsNoDocument()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var track = await SeedTrackAsync(db);
        await using (var write = db.NewContext())
        {
            await new TrackAnalysisRepository(write).AddAsync(ReadyChords(track.Id), Ct);
        }

        foreach (var hostile in new[] { "{\"$type\":\"evil\"}", "not json", "{\"$type\":\"chords\",\"Segments\":" + new string('[', 100) })
        {
            await db.ScalarAsync($"UPDATE TrackAnalyses SET DocumentJson = '{hostile}'");
            await using var read = db.NewContext();
            (await new TrackAnalysisRepository(read).FindAsync(track.Id, AnalysisKind.Chords, Ct))!.Document.ShouldBeNull(hostile);
        }
    }

    [Fact]
    public void DocumentComparer_ComparesByReference()
    {
        var chart = (ChordChart)ReadyChords(Guid.NewGuid()).Document!;

        AnalysisDocumentJson.Comparer.Equals(chart, chart).ShouldBeTrue();
        AnalysisDocumentJson.Comparer.Equals(chart, chart with { }).ShouldBeFalse();
        AnalysisDocumentJson.Comparer.GetHashCode(chart).ShouldBe(AnalysisDocumentJson.Comparer.GetHashCode(chart));
        AnalysisDocumentJson.Comparer.Snapshot(chart).ShouldBeSameAs(chart);
    }

    [Fact]
    public async Task ScoreItemsWithTablatureAndPartFacts_RoundTrip_AndOlderItemsLoadUnchanged()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var track = await SeedTrackAsync(db);
        var tab = Score(track.Id, new TablatureSettings(TuningId.GuitarDropD, 2, TabLayout.TabOnly), null);
        tab.CompleteScore(Files, Facts() with
        {
            Tablature = new TablatureFacts { Tuning = TuningId.GuitarDropD, Strings = 6, Capo = 2, Layout = TabLayout.TabOnly, OctaveMoved = 3, NotesDropped = 1, WideStretches = 2 },
            Part = new PartFacts { Instrument = WrittenInstrument.AltoSaxEb, Name = "Alto Saxophone in E-flat", Interval = "M6", WrittenKey = "A", Clef = ClefChoice.Treble, OutOfRange = 4 },
            ChordsIncluded = true,
        }, T0);
        var old = Score(track.Id, null, null);
        old.CompleteScore(Files, Facts(), T0);
        await using (var write = db.NewContext())
        {
            write.Assets.AddRange(tab, old);
            await write.SaveChangesAsync(Ct);
        }

        await using var read = db.NewContext();
        var loaded = await read.Assets.AsNoTracking().ToDictionaryAsync(asset => asset.Id, Ct);
        loaded[tab.Id].Score!.Tablature.ShouldBe(tab.Score!.Tablature);
        loaded[tab.Id].Score!.Part.ShouldBe(tab.Score!.Part);
        loaded[tab.Id].Score!.ChordsIncluded.ShouldBeTrue();
        loaded[tab.Id].Provenance.ScoreRecipe!.Tablature.ShouldBe(new TablatureSettings(TuningId.GuitarDropD, 2, TabLayout.TabOnly));
        loaded[tab.Id].Provenance.ScoreRecipe!.IncludeChords.ShouldBeTrue();
        (loaded[old.Id].Score!.Tablature, loaded[old.Id].Score!.Part, loaded[old.Id].Score!.ChordsIncluded).ShouldBe((null, null, false));
        (loaded[old.Id].Provenance.ScoreRecipe!.Part, loaded[old.Id].Provenance.ScoreRecipe!.IncludeChords).ShouldBe((null, false));
    }

    [Fact]
    public async Task A008ScoreRowWithoutThe016Fields_Loads()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var track = await SeedTrackAsync(db);
        var old = Score(track.Id, null, null);
        old.CompleteScore(Files, Facts(), T0);
        await using (var write = db.NewContext())
        {
            write.Assets.Add(old);
            await write.SaveChangesAsync(Ct);
        }

        await db.ScalarAsync("UPDATE Assets SET Score = json_remove(Score, '$.Tablature', '$.Part', '$.ChordsIncluded')");
        await using var read = db.NewContext();
        var loaded = await read.Assets.AsNoTracking().SingleAsync(Ct);

        (loaded.Score!.Tablature, loaded.Score.Part, loaded.Score.ChordsIncluded, loaded.Score.Bars).ShouldBe((null, null, false, 12));
    }

    private static readonly AssetFile[] Files = [new AssetFile("pdf", "score.pdf", "application/pdf", 100, new string('a', 64))];

    private static ScoreFacts Facts() => new()
    {
        Profile = ScoreProfile.Guitar, Engine = ScoreEngine.BasicPitch, Bpm = 120, TimeSignature = "4/4", Bars = 12, Notes = 40, Pages = 1, Resolution = ScoreResolution.Sixteenth,
    };

    private static Asset Score(Guid trackId, TablatureSettings? tablature, PartSettings? part)
    {
        var asset = Asset.CreateScore(Guid.NewGuid(), "Song - guitar (sheet music)", new Provenance
        {
            Origin = AssetOrigin.Transcription,
            TrackId = trackId,
            TrackTitle = "Song",
            Contents = "guitar, sheet music",
            ScoreRecipe = new ScoreRecipe
            {
                Source = ScoreSource.FromChannel(trackId, "guitar"),
                Profile = ScoreProfile.Guitar,
                Resolution = ScoreResolution.Sixteenth,
                Tablature = tablature,
                Part = part,
                IncludeChords = tablature is not null,
            },
        }, T0);
        asset.AssignJob(JobId, T0);
        return asset;
    }

    private static TrackAnalysis Queued(Guid trackId, AnalysisKind kind)
    {
        var analysis = TrackAnalysis.Create(trackId, kind, T0);
        analysis.Queue(Run, T0);
        return analysis;
    }

    private static TrackAnalysis ReadyChords(Guid trackId)
    {
        var analysis = Queued(trackId, AnalysisKind.Chords);
        analysis.Complete(
            new ChordChart
            {
                Engine = "btc",
                Vocabulary = ChordVocabulary.Simple,
                Key = new SongKey(0, KeyMode.Major, 0.81, new MusicalKey(9, KeyMode.Minor)),
                KeyRegions = [new KeyRegion(500, 16500, 0, KeyMode.Major, 0.84), new KeyRegion(16500, 32500, 2, KeyMode.Major, 0.77)],
                BeatSnapped = true,
                Segments =
                [
                    new ChordSegment { StartMs = 0, EndMs = 500, Label = "N", Confidence = 0.95 },
                    new ChordSegment { StartMs = 500, EndMs = 2500, Label = "C:maj", Root = 0, Quality = ChordQuality.Maj, Confidence = 0.9 },
                ],
                Warnings = [new AnalysisWarning(AnalysisWarningCode.KeyUncertain, "The key is uncertain (confidence 45 %).")],
            },
            new JobDevice(ProcessingDeviceKind.Gpu, "RTX", false, null),
            T0.AddMinutes(1));
        return analysis;
    }

    private static async Task<Track> SeedTrackAsync(SqliteTestDatabase db, string videoId = "dQw4w9WgXcQ")
    {
        var track = TestData.CompletedTrack(videoId);
        await db.SeedAsync(track);
        return track;
    }
}
