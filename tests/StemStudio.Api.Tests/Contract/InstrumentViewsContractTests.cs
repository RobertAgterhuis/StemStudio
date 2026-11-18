using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using StemStudio.Api.LibraryCare.Archive;
using StemStudio.Contracts;
using StemStudio.Contracts.InstrumentViews;
using StemStudio.Contracts.Scores;
using StemStudio.Contracts.Tracks;
using static StemStudio.Api.Tests.Contract.ContractJson;

namespace StemStudio.Api.Tests.Contract;

/// <summary>
/// Skeleton tests (architect, 016 Phase 0, T003): backend-api 1.15.0 (<c>specs/016-instrument-views/contracts/backend-api.openapi.yaml</c>)
/// is 015's 1.14.0 plus exactly the 016 delta (DELTA.md 2 and "Phase 0 outcome"), and the InstrumentViews DTOs, routes and limits match it.
/// </summary>
public sealed class InstrumentViewsContractTests
{
    internal static readonly string[] Operations =
    [
        "getChords", "detectChords", "deleteChords", "editChordSegment", "getChordSheet", "downloadChordPro", "getKey",
        "getCapoSuggestions", "getDrumHits", "detectDrumHits", "deleteDrumHits", "getVocalPitch", "analyseVocalPitch",
        "deleteVocalPitch", "getInstrumentCatalogue",
    ];

    private const string ExternalRef = "../../010-lyrics-to-song/contracts/backend-api.openapi.yaml#";

    public static TheoryData<string, string[]> Changed1140Entries => new()
    {
        { "paths", ["/api/assets"] },
        { "components/schemas", ["Track", "CreateScoreRequest", "ScoreRecipe", "ScoreOptions", "ScoreFacts", "ScoreProfile", "ScoreWarningCode"] },
        { "components/responses", ["ScoreConflict", "ScoreUnprocessable"] },
        { "components/examples", [] },
        { "components/parameters", [] },
        { "components/headers", [] },
    };

    public static TheoryData<string, Type> OperationExamples => new()
    {
        { "getChords", typeof(TrackAnalysisDto<ChordChartDocumentDto>) },
        { "getChordSheet", typeof(ChordSheetDto) },
        { "getKey", typeof(KeyViewDto) },
        { "getCapoSuggestions", typeof(CapoSuggestionsDto) },
    };

    public static TheoryData<string, object> SampleInstances => new()
    {
        { "DetectChordsRequest", new DetectChordsRequest { Vocabulary = ChordVocabularyDto.Full, Engine = ChordEngineDto.ChromaHmm, OverwriteEdits = true } },
        { "EditChordRequest", new EditChordRequest { Label = "A:min7/b7" } },
        { "TrackKeySummary", new TrackKeySummaryDto { Tonic = 3, Mode = KeyModeDto.Major, Confidence = 0.8, Display = "Eb major", Regions = 2 } },
        { "InstrumentViewsStatus", InstrumentViewsStatusDto.None },
        { "DrumHitsAnalysis", DrumAnalysis() },
        { "VocalPitchAnalysis", PitchAnalysis() },
        { "InstrumentCatalogue", Catalogue() },
        { "ScoreTablature", new ScoreTablatureDto { Tuning = TuningIdDto.GuitarDropD, Capo = 2 } },
        { "ScorePart", new ScorePartDto { Instrument = WrittenInstrumentDto.AltoSaxEb } },
        { "TablatureFacts", new TablatureFactsDto { Tuning = TuningIdDto.Bass5Standard, Strings = 5, Capo = 0, Layout = TabLayoutDto.TabOnly, OctaveMoved = 1, NotesDropped = 0, WideStretches = 0 } },
        { "PartFacts", new PartFactsDto { Instrument = WrittenInstrumentDto.ClarinetBb, Name = "Clarinet in B-flat", Interval = "M2", WrittenKey = "D major", Clef = "treble", OutOfRange = 0 } },
    };

    [Fact]
    public void Backend_IsLabelled1150AndListsTheMergedDeltas()
    {
        var info = OpenApiContract.Backend.Root["info"]!;

        info["version"]!.GetValue<string>().ShouldBe("1.15.0");
        Strings(info["x-stemstudio-contains"]!).TakeLast(3).ShouldBe(["1.13.0", "1.14.0", "1.15.0"]);
        ManifestBuilder.BackendApiVersion.ShouldBe("1.15.0");
    }

    [Theory]
    [MemberData(nameof(Changed1140Entries))]
    public void Backend1150_Contains1140UnchangedExceptTheAdditions(string section, string[] changed)
    {
        ChangedEntries(Backend015, OpenApiContract.Backend, section).ShouldBe(changed, ignoreOrder: true);
    }

    [Fact]
    public void Backend1150_AddsExactlyTheFifteenOperations()
    {
        var added = OpenApiContract.Backend.Operations.Select(OperationOf).Except(Backend015.Operations.Select(OperationOf)).ToList();

        added.ShouldBe(Operations, ignoreOrder: true);
        Backend016Delta.Operations.Select(OperationOf).ShouldBe(Operations, ignoreOrder: true);
    }

    [Fact]
    public void DeltaSchemas_AreMergedUnchanged()
    {
        var merged = Section(OpenApiContract.Backend.Root, "components/schemas");
        var changed = Section(Backend016Delta.Root, "components/schemas")
            .Where(s => !s.Key.EndsWith("Extension", StringComparison.Ordinal))
            .Where(s => s.Value!.ToJsonString().Replace(ExternalRef, "#", StringComparison.Ordinal) != merged[s.Key]?.ToJsonString())
            .Select(s => s.Key);

        changed.ShouldBeEmpty();
    }

    [Fact]
    public void Extensions_AreOptionalPropertiesOfTheBaseSchemas()
    {
        var merged = Section(OpenApiContract.Backend.Root, "components/schemas");
        var expected = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["Track"] = ["instrumentViews", "key"],
            ["CreateScoreRequest"] = ["tablature", "part", "includeChords"],
            ["ScoreRecipe"] = ["tablature", "part", "includeChords"],
            ["ScoreOptions"] = ["tablature", "parts", "chords"],
            ["ScoreFacts"] = ["tablature", "part", "chordsIncluded"],
        };

        foreach (var (schema, properties) in expected)
        {
            var props = merged[schema]!["properties"]!.AsObject();
            properties.ShouldAllBe(p => props.ContainsKey(p), schema);
            Strings(merged[schema]!["required"]!).Intersect(properties).ShouldBeEmpty(schema);
        }

        Strings(merged["ScoreProfile"]!["enum"]!).TakeLast(3).ShouldBe(["strings", "brass", "woodwinds"]);
    }

    [Theory]
    [MemberData(nameof(OperationExamples))]
    public void OperationExample_RoundTripsAndValidates(string operationId, Type type)
    {
        var response = OpenApiContract.Backend.Response(operationId, 200)!["content"]!["application/json"]!;
        var json = response["example"]!.DeepClone();
        var again = JsonSerializer.SerializeToNode(json.Deserialize(type, Web), type, Web)!;

        JsonNode.DeepEquals(WithoutAnyTimes(json), WithoutAnyTimes(again)).ShouldBeTrue(again.ToJsonString());
        SchemaValidator.Validate(OpenApiContract.Backend, OpenApiContract.Backend.Resolve(response["schema"]!), again).ShouldBeEmpty();
    }

    [Theory]
    [MemberData(nameof(SampleInstances))]
    public void ConstructedDtos_ValidateAgainstTheirSchemas(string schema, object dto)
    {
        var node = JsonSerializer.SerializeToNode(dto, dto.GetType(), Web);

        SchemaValidator.Validate(OpenApiContract.Backend, Schema(schema), node).ShouldBeEmpty();
    }

    [Fact]
    public void ExistingDtos_WithoutThe016Fields_SerializeAs1140()
    {
        var facts = JsonSerializer.SerializeToNode(new ScoreFactsDto { Profile = ScoreProfileDto.Bass, Engine = ScoreEngineDto.BasicPitch, Bpm = 120, TimeSignature = "4/4", Bars = 1, Notes = 1, Pages = 1, Resolution = ScoreResolutionDto.Sixteenth }, Web)!;

        facts.AsObject().ContainsKey("tablature").ShouldBeFalse();
        facts.AsObject().ContainsKey("chordsIncluded").ShouldBeFalse();
    }

    [Fact]
    public void ComponentEnums_MatchTheirSchemas()
    {
        AssertEnumsMatch([
            (typeof(AnalysisStatusDto), "AnalysisStatus"), (typeof(ChordQualityDto), "ChordQuality"), (typeof(InstrumentFamilyDto), "InstrumentFamily"),
            (typeof(TuningIdDto), "TuningIdBackend"), (typeof(WrittenInstrumentDto), "WrittenInstrumentBackend"), (typeof(ClefChoiceDto), "ClefChoiceBackend"),
            (typeof(ScoreProfileDto), "ScoreProfile"), (typeof(ScoreWarningCodeDto), "ScoreWarningCode"),
        ]);
    }

    [Fact]
    public void HartePattern_IsTheContractPattern()
    {
        var pattern = Schema("EditChordRequest")["properties"]!["label"]!["pattern"]!.GetValue<string>();

        pattern.ShouldBe(ContractLimits.HarteLabelPattern);
        Regex.IsMatch("Bb:hdim7/b7", pattern, RegexOptions.None, TimeSpan.FromSeconds(1)).ShouldBeTrue();
        Regex.IsMatch("H:maj", pattern, RegexOptions.None, TimeSpan.FromSeconds(1)).ShouldBeFalse();
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public void Routes_MatchContractPaths(string route, string method)
    {
        OpenApiContract.Backend.Operations.ShouldContain(op => op.Method == method && PathTemplate(op.Path).IsMatch(route), route);
    }

    public static TheoryData<string, string> Routes()
    {
        var id = Guid.NewGuid();
        return new()
        {
            { ApiRoutes.Chords(id), "get" }, { ApiRoutes.Chords(id), "post" }, { ApiRoutes.Chords(id), "delete" },
            { ApiRoutes.ChordSegment(id, 12), "put" }, { ApiRoutes.ChordSheet(id, -2, 3), "get" }, { ApiRoutes.ChordPro(id, 0, 0), "get" },
            { ApiRoutes.CapoSuggestions(id, 3), "get" }, { ApiRoutes.Key(id, 2, "alto-sax-eb"), "get" }, { ApiRoutes.Key(id, 0, null), "get" },
            { ApiRoutes.DrumHits(id), "post" }, { ApiRoutes.VocalPitch(id), "delete" }, { ApiRoutes.InstrumentCatalogue, "get" },
        };
    }

    [Fact]
    public void Limits_MatchTheSchemas()
    {
        Schema("ChordChartDocument")["properties"]!["segments"]!["maxItems"]!.GetValue<double>().ShouldBe(ContractLimits.MaxChordSegments);
        Schema("ChordChartDocument")["properties"]!["keyRegions"]!["maxItems"]!.GetValue<double>().ShouldBe(ContractLimits.MaxKeyRegions);
        Schema("ChordSheet")["properties"]!["rows"]!["maxItems"]!.GetValue<double>().ShouldBe(ContractLimits.MaxChordSheetRows);
        Schema("ChordSegment")["properties"]!["label"]!["maxLength"]!.GetValue<double>().ShouldBe(ContractLimits.ChordLabelMaxLength);
        Schema("ScoreTablature")["properties"]!["capo"]!["maximum"]!.GetValue<double>().ShouldBe(ContractLimits.CapoMax);
        new SingingTolerancesDto().ShouldBe(new SingingTolerancesDto { Beginner = 100, Advanced = 50, Expert = 25, Min = 10, Max = 200, Step = 5 });
    }

    private static string OperationOf((string Path, string Method, JsonObject Operation) op) => OperationId(op.Operation);

    private static Regex PathTemplate(string path) =>
        new("^" + Regex.Replace(path, @"\{[^}]+\}", "[^/?]+") + @"(\?.*)?$", RegexOptions.None, TimeSpan.FromSeconds(1));

    private static TrackAnalysisDto<DrumHitsDocumentDto> DrumAnalysis() => new()
    {
        Kind = AnalysisKindDto.DrumHits,
        Status = AnalysisStatusDto.Ready,
        Reason = null,
        Progress = 1,
        Engine = AnalysisEngineDto.DrumOnsets,
        Device = null,
        EstimateSeconds = null,
        UpdatedAt = DateTimeOffset.UnixEpoch,
        Limits = "Hits come from the drum detail stems.",
        Document = new() { Pieces = [DrumPieceDto.Kick], Hits = [new() { TimeMs = 500, Piece = DrumPieceDto.Kick, Strength = 0.9 }] },
    };

    private static TrackAnalysisDto<VocalPitchDocumentDto> PitchAnalysis() => new()
    {
        Kind = AnalysisKindDto.VocalPitch,
        Status = AnalysisStatusDto.Queued,
        Reason = null,
        Progress = 0,
        Engine = null,
        Device = new JobDeviceDto(ProcessingDeviceKindDto.Cpu, null, false, null),
        EstimateSeconds = 95,
        UpdatedAt = DateTimeOffset.UnixEpoch,
        Limits = "The melody comes from the lead vocal stem.",
        Document = new() { Frames = [0, 6900, 6950], Source = VocalSourceDto.VocalsLead, VoicedRatio = 0.66, Range = new() { LowCents = 6900, HighCents = 6950 }, Warnings = [] },
    };

    private static InstrumentCatalogueDto Catalogue() => new()
    {
        Families = [new() { Id = InstrumentFamilyDto.Orchestral, Label = "Strings, brass and woodwinds", Stems = ["other"], DetailNames = ["strings", "brass", "woodwinds"], View = InstrumentViewDto.Part }],
        Tunings = [new() { Id = TuningIdDto.GuitarStandard, Instrument = "guitar", Label = "Standard (E A D G B E)", Pitches = [40, 45, 50, 55, 59, 64] }],
        Instruments = [new() { Id = WrittenInstrumentDto.Concert, Name = string.Empty, KeyName = "C", Interval = "P1", DefaultClef = "auto", LowMidi = 0, HighMidi = 127 }],
        Clefs = [ClefChoiceDto.Auto, ClefChoiceDto.Treble8vb],
    };
}
