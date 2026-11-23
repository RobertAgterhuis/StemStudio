using System.Text.Json;
using StemStudio.Contracts;
using StemStudio.Contracts.InstrumentViews;
using StemStudio.Contracts.Scores;

namespace StemStudio.Web.Tests.Fakes;

/// <summary>
/// Consumer-driven fakes of the 016 backend operations (backend-api 1.15.0): the JSON documents are the contract
/// examples verbatim; the builders derive variants from them.
/// </summary>
public static class InstrumentViewsData
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary><c>getChords</c> 200 example.</summary>
    public const string ChordsJson = """
        {"kind":"chords","status":"ready","reason":null,"progress":1,"engine":"btc",
         "device":{"kind":"cpu","name":null,"cpuFallback":false,"fallbackReason":null},"estimateSeconds":null,
         "updatedAt":"2026-11-16T10:00:06Z","limits":"Chord detection is a practice aid (about 80 % correct on pop songs); check by ear.",
         "document":{"vocabulary":"simple","engine":"btc","key":{"tonic":0,"mode":"major","confidence":0.81,"alternative":null},
           "keyRegions":[{"startMs":500,"endMs":16500,"tonic":0,"mode":"major","confidence":0.84}],
           "preferFlats":false,"beatSnapped":true,"noChords":false,"hasEdits":false,
           "segments":[{"startMs":0,"endMs":500,"label":"N","root":null,"quality":null,"bass":null,"confidence":0.95,"edited":false},
                       {"startMs":500,"endMs":2500,"label":"C:maj","root":0,"quality":"maj","bass":null,"confidence":0.9,"edited":false}],
           "warnings":[]}}
        """;

    /// <summary><c>getChordSheet</c> 200 example.</summary>
    public const string SheetJson = """
        {"transpose":0,"capo":2,"rows":[{"text":"Fake line one","chords":[{"charIndex":0,"symbol":"A","lead":false},{"charIndex":5,"symbol":"E","lead":false}]},
                                        {"text":"","chords":[{"charIndex":0,"symbol":"F#m","lead":false}]}]}
        """;

    /// <summary><c>getKey</c> 200 example.</summary>
    public const string KeyJson = """
        {"status":"ready","transpose":2,"key":{"tonic":0,"mode":"major","confidence":0.81,"alternative":null},
         "display":"D major (original C major, +2)","writtenKey":null,
         "regions":[{"startMs":500,"endMs":16500,"startBar":1,"tonic":0,"mode":"major","confidence":0.84,"display":"D major"},
                    {"startMs":16500,"endMs":32500,"startBar":9,"tonic":2,"mode":"major","confidence":0.77,"display":"E major"}]}
        """;

    /// <summary><c>getCapoSuggestions</c> 200 example.</summary>
    public const string CapoJson = """
        {"transpose":0,"shortcut":null,"suggestions":[{"capo":3,"shapeKey":"C major","openShare":1.0,"fewOpenShapes":false},
         {"capo":1,"shapeKey":"D major","openShare":0.75,"fewOpenShapes":false},{"capo":6,"shapeKey":"A major","openShare":0.75,"fewOpenShapes":false}]}
        """;

    /// <summary>The chords example as DTO.</summary>
    /// <returns>A ready chords analysis.</returns>
    public static TrackAnalysisDto<ChordChartDocumentDto> Chords() => Parse<TrackAnalysisDto<ChordChartDocumentDto>>(ChordsJson);

    /// <summary>A chords analysis in another status, without a document unless ready or stale.</summary>
    /// <param name="status">The status.</param>
    /// <param name="progress">Progress 0..1.</param>
    /// <returns>The analysis.</returns>
    public static TrackAnalysisDto<ChordChartDocumentDto> Chords(AnalysisStatusDto status, double progress = 0) => Chords() with
    {
        Status = status,
        Progress = progress,
        Document = status is AnalysisStatusDto.Ready or AnalysisStatusDto.Stale ? Chords().Document : null,
        Reason = status == AnalysisStatusDto.Failed ? "The worker stopped." : null,
        EstimateSeconds = status is AnalysisStatusDto.Queued or AnalysisStatusDto.Running ? 12 : null,
    };

    /// <summary>A stale chords analysis whose first segment the user corrected (review M2): the backend still serves it.</summary>
    /// <returns>The analysis.</returns>
    public static TrackAnalysisDto<ChordChartDocumentDto> StaleEditedChords()
    {
        var stale = Chords(AnalysisStatusDto.Stale);
        var segments = stale.Document!.Segments;
        return stale with
        {
            Reason = "The drum stems were split again.",
            Document = stale.Document with { HasEdits = true, Segments = [segments[0] with { Edited = true }, .. segments.Skip(1)] },
        };
    }

    /// <summary>The sheet example as DTO.</summary>
    /// <returns>The sheet.</returns>
    public static ChordSheetDto Sheet() => Parse<ChordSheetDto>(SheetJson);

    /// <summary>The key example as DTO.</summary>
    /// <returns>The key view.</returns>
    public static KeyViewDto Key() => Parse<KeyViewDto>(KeyJson);

    /// <summary>The capo suggestions example as DTO.</summary>
    /// <returns>The suggestions.</returns>
    public static CapoSuggestionsDto Capo() => Parse<CapoSuggestionsDto>(CapoJson);

    /// <summary>A drum hits analysis (kick, hi-hat, snare pattern).</summary>
    /// <param name="status">The status.</param>
    /// <returns>The analysis.</returns>
    public static TrackAnalysisDto<DrumHitsDocumentDto> DrumHits(AnalysisStatusDto status = AnalysisStatusDto.Ready) => new()
    {
        Kind = AnalysisKindDto.DrumHits,
        Status = status,
        Reason = null,
        Progress = status == AnalysisStatusDto.Ready ? 1 : 0,
        Engine = AnalysisEngineDto.DrumOnsets,
        Device = null,
        EstimateSeconds = null,
        UpdatedAt = DateTimeOffset.Parse("2026-11-16T10:00:06Z", System.Globalization.CultureInfo.InvariantCulture),
        Limits = "Drum hits are found from the split drum stems; fast fills may be missed.",
        Document = status is AnalysisStatusDto.Ready or AnalysisStatusDto.Stale
            ? new DrumHitsDocumentDto
            {
                Pieces = [DrumPieceDto.Kick, DrumPieceDto.Snare, DrumPieceDto.Hihat],
                Hits =
                [
                    new DrumHitDto { TimeMs = 500, Piece = DrumPieceDto.Kick, Strength = 0.9 },
                    new DrumHitDto { TimeMs = 500, Piece = DrumPieceDto.Hihat, Strength = 0.5 },
                    new DrumHitDto { TimeMs = 1000, Piece = DrumPieceDto.Snare, Strength = 0.8 },
                ],
            }
            : null,
    };

    /// <summary>A vocal pitch analysis.</summary>
    /// <param name="status">The status.</param>
    /// <returns>The analysis.</returns>
    public static TrackAnalysisDto<VocalPitchDocumentDto> VocalPitch(AnalysisStatusDto status = AnalysisStatusDto.Ready) => new()
    {
        Kind = AnalysisKindDto.VocalPitch,
        Status = status,
        Reason = null,
        Progress = status == AnalysisStatusDto.Ready ? 1 : 0,
        Engine = AnalysisEngineDto.Pyin,
        Device = null,
        EstimateSeconds = status == AnalysisStatusDto.Queued ? 8 : null,
        UpdatedAt = DateTimeOffset.Parse("2026-11-16T10:00:06Z", System.Globalization.CultureInfo.InvariantCulture),
        Limits = "The reference melody comes from the vocal stem; bleed can add wrong notes.",
        Document = status == AnalysisStatusDto.Ready
            ? new VocalPitchDocumentDto
            {
                Frames = [-32768, 4500, 4510, 4700],
                Source = VocalSourceDto.VocalsLead,
                VoicedRatio = 0.75,
                Range = new PitchRangeDto { LowCents = 4500, HighCents = 4700 },
                Warnings = [],
            }
            : null,
    };

    /// <summary>A catalogue with every family, two guitar and two bass tunings, three instruments and the clefs.</summary>
    /// <returns>The catalogue.</returns>
    public static InstrumentCatalogueDto Catalogue() => new()
    {
        Families =
        [
            new() { Id = InstrumentFamilyDto.Vocals, Label = "Vocals", Stems = ["vocals"], DetailNames = ["lead", "backing"], View = InstrumentViewDto.Singing },
            new() { Id = InstrumentFamilyDto.Drums, Label = "Drums", Stems = ["drums"], DetailNames = ["kick", "snare", "toms", "hihat", "cymbals", "percussion"], View = InstrumentViewDto.DrumTrainer },
            new() { Id = InstrumentFamilyDto.Bass, Label = "Bass", Stems = ["bass"], DetailNames = [], View = InstrumentViewDto.Tablature },
            new() { Id = InstrumentFamilyDto.Guitar, Label = "Guitar", Stems = ["guitar"], DetailNames = ["acoustic-guitar", "electric-guitar"], View = InstrumentViewDto.Tablature },
            new() { Id = InstrumentFamilyDto.Keys, Label = "Keys", Stems = ["piano"], DetailNames = ["keys", "synth"], View = InstrumentViewDto.Chords },
            new() { Id = InstrumentFamilyDto.Orchestral, Label = "Strings, brass and woodwinds", Stems = ["other"], DetailNames = ["strings", "brass", "woodwinds"], View = InstrumentViewDto.Part },
        ],
        Tunings =
        [
            Tuning(TuningIdDto.GuitarStandard, "guitar", "Standard (E A D G B E)", [40, 45, 50, 55, 59, 64]),
            Tuning(TuningIdDto.GuitarDropD, "guitar", "Drop D (D A D G B E)", [38, 45, 50, 55, 59, 64]),
            Tuning(TuningIdDto.GuitarDadgad, "guitar", "DADGAD", [38, 45, 50, 55, 57, 62]),
            Tuning(TuningIdDto.Bass4Standard, "bass", "4-string standard (E A D G)", [28, 33, 38, 43]),
            Tuning(TuningIdDto.Bass5Standard, "bass", "5-string standard (B E A D G)", [23, 28, 33, 38, 43]),
        ],
        Instruments =
        [
            new() { Id = WrittenInstrumentDto.Concert, Name = string.Empty, KeyName = "C", Interval = "P1", DefaultClef = "auto", LowMidi = 0, HighMidi = 127 },
            new() { Id = WrittenInstrumentDto.AltoSaxEb, Name = "Alto Saxophone in E-flat", KeyName = "E-flat", Interval = "M6", DefaultClef = "treble", LowMidi = 49, HighMidi = 81 },
            new() { Id = WrittenInstrumentDto.Viola, Name = "Viola", KeyName = "C", Interval = "P1", DefaultClef = "alto", LowMidi = 48, HighMidi = 88 },
        ],
        Clefs = Enum.GetValues<ClefChoiceDto>(),
    };

    /// <summary>Score options of a guitar source with tablature, parts and chords.</summary>
    /// <param name="profile">The suggested profile.</param>
    /// <param name="chordsAvailable">Whether "include chords" is offered.</param>
    /// <returns>The options.</returns>
    public static ScoreOptionsDto ScoreOptions(ScoreProfileDto profile = ScoreProfileDto.Guitar, bool chordsAvailable = true) => new()
    {
        Source = Contracts.Assets.AudioSourceDto.FromChannel(TestData.TrackId, StemNames.Guitar),
        DurationSeconds = 213,
        SuggestedProfile = profile,
        AllowedProfiles = [profile],
        GridStatus = GridStatusDto.Reliable,
        DefaultQuality = ScoreQualityDto.Accurate,
        Attribution = "Transcription by Basic Pitch.",
        Tablature = new ScoreTablatureOptionsDto
        {
            Available = true,
            DefaultTuning = TuningIdDto.GuitarStandard,
            Tunings = [TuningIdDto.GuitarStandard, TuningIdDto.GuitarDropD, TuningIdDto.GuitarDadgad],
            CapoAllowed = true,
        },
        Parts = new ScorePartOptionsDto { Available = true, DefaultInstrument = WrittenInstrumentDto.Concert, ClefAllowed = true },
        Chords = new ScoreChordOptionsDto { Available = chordsAvailable },
    };

    /// <summary>Serialises with the contract options.</summary>
    /// <typeparam name="T">The type.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns>JSON.</returns>
    public static string ToJson<T>(T value) => JsonSerializer.Serialize(value, Json);

    private static T Parse<T>(string json) => JsonSerializer.Deserialize<T>(json, Json)!;

    private static TuningDto Tuning(TuningIdDto id, string instrument, string label, int[] pitches) =>
        new() { Id = id, Instrument = instrument, Label = label, Pitches = pitches };
}
