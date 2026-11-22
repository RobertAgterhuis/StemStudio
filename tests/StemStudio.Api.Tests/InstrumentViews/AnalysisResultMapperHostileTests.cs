using StemStudio.Api.InstrumentViews;
using StemStudio.Api.Tests.Worker;
using StemStudio.Domain.InstrumentViews;

namespace StemStudio.Api.Tests.InstrumentViews;

/// <summary>Feature 016 T133: worker results outside the 1.9.0 contract are refused, never stored (data-model 7.6).</summary>
public sealed class AnalysisResultMapperHostileTests
{
    private const string Segment = """{"startMs": 0, "endMs": 500, "label": "C:maj", "root": 0, "quality": "maj", "confidence": 0.9}""";

    private static string Chords(string fields) => $$"""{"kind": "chords", "engine": "btc", "vocabulary": "simple", "segments": [{{Segment}}]{{fields}}}""";

    private static AnalysisDocument? Map(AnalysisKind kind, string json) => AnalysisResultMapper.ToDocument(kind, FakeWorkerAnalysisClient.Result(json));

    [Theory]
    [InlineData("""{"kind": "chords", "engine": "madmom", "segments": []}""")]
    [InlineData("""{"kind": "chords", "engine": "btc", "segments": [{"startMs": 500, "endMs": 500, "label": "C:maj", "confidence": 0.9}]}""")]
    [InlineData("""{"kind": "chords", "engine": "btc", "segments": [{"startMs": 0, "endMs": 500, "label": "C:power", "confidence": 0.9}]}""")]
    [InlineData("""{"kind": "chords", "engine": "btc", "segments": [{"startMs": 0, "endMs": 500, "label": "C:maj", "quality": "power", "confidence": 0.9}]}""")]
    [InlineData("""{"kind": "chords", "engine": "btc", "segments": [{"startMs": 0, "endMs": 500, "label": "C:maj", "root": 12, "confidence": 0.9}]}""")]
    [InlineData("""{"kind": "chords", "engine": "btc", "segments": [{"startMs": 0, "endMs": 500, "label": "C:maj", "bass": -1, "confidence": 0.9}]}""")]
    [InlineData("""{"kind": "chords", "engine": "btc", "segments": [{"startMs": 0, "endMs": 500, "label": "C:maj", "confidence": 1.5}]}""")]
    public void BrokenChordSegments_AreRefused(string json) => Map(AnalysisKind.Chords, json).ShouldBeNull();

    [Theory]
    [InlineData(""", "key": {"tonic": 0, "mode": "dorian", "confidence": 0.9}""")]
    [InlineData(""", "key": {"tonic": 0, "mode": "major", "confidence": 0.9, "alternative": {"tonic": 13, "mode": "minor"}}""")]
    [InlineData(""", "keyRegions": [{"startMs": 0, "endMs": 500, "tonic": 0, "mode": "major", "confidence": -0.1}]""")]
    public void BrokenKeys_AreRefused(string fields) => Map(AnalysisKind.Chords, Chords(fields)).ShouldBeNull();

    [Fact]
    public void AValidChart_KeepsTheKnownWarnings_ClipsTheirTextAndDropsUnknownCodes()
    {
        var text = new string('x', 400);
        const string Key = """, "key": {"tonic": 9, "mode": "minor", "confidence": 0.4, "alternative": {"tonic": 0, "mode": "major"}}""";
        var warnings = """, "warnings": [{"code": "key-uncertain", "text": """ + $"\"{text}\"" + """}, {"code": "made-up", "text": "x"}, {"code": "few-chords"}]""";
        var json = Chords(Key + warnings);

        var chart = Map(AnalysisKind.Chords, json).ShouldBeOfType<ChordChart>();

        chart.Key!.Alternative.ShouldBe(new MusicalKey(0, Domain.Scores.KeyMode.Major));
        chart.Warnings.Select(warning => (warning.Code, warning.Text.Length)).ShouldBe([(AnalysisWarningCode.KeyUncertain, 300), (AnalysisWarningCode.FewChords, 0)]);
    }

    [Theory]
    [InlineData("""{"kind": "drum-hits", "engine": "librosa", "pieces": [], "hits": []}""")]
    [InlineData("""{"kind": "drum-hits", "engine": "drum-onsets", "pieces": ["cowbell"], "hits": []}""")]
    [InlineData("""{"kind": "drum-hits", "engine": "drum-onsets", "pieces": ["kick"], "hits": [{"timeMs": -1, "piece": "kick", "strength": 0.5}]}""")]
    [InlineData("""{"kind": "drum-hits", "engine": "drum-onsets", "pieces": ["kick"], "hits": [{"timeMs": 10, "piece": "kick", "strength": 2}]}""")]
    public void BrokenDrumHits_AreRefused(string json) => Map(AnalysisKind.DrumHits, json).ShouldBeNull();

    [Theory]
    [InlineData("""{"kind": "vocal-pitch", "engine": "crepe", "hopMs": 10, "frames": [], "source": "vocals"}""")]
    [InlineData("""{"kind": "vocal-pitch", "engine": "pyin", "hopMs": 20, "frames": [], "source": "vocals"}""")]
    [InlineData("""{"kind": "vocal-pitch", "engine": "pyin", "hopMs": 10, "frames": [12701], "source": "vocals"}""")]
    [InlineData("""{"kind": "vocal-pitch", "engine": "pyin", "hopMs": 10, "frames": [0], "source": "vocals.backing"}""")]
    [InlineData("""{"kind": "vocal-pitch", "engine": "pyin", "hopMs": 10, "frames": [0], "source": "vocals", "voicedRatio": 1.2}""")]
    public void BrokenMelodies_AreRefused(string json) => Map(AnalysisKind.VocalPitch, json).ShouldBeNull();

    [Fact]
    public void AMelodyWithoutTheOptionalFields_HasTheirDefaults()
    {
        var pitch = Map(AnalysisKind.VocalPitch, """{"kind": "vocal-pitch", "engine": "yin", "source": "vocals"}""").ShouldBeOfType<VocalPitchDocument>();

        (pitch.Frames.Count, pitch.VoicedRatio, pitch.Range, pitch.Warnings.Count).ShouldBe((0, 0.0, null, 0));
    }

    [Fact]
    public void TooManyItems_AreRefused()
    {
        var frames = string.Join(',', Enumerable.Repeat("0", Contracts.ContractLimits.MaxPitchFrames + 1));

        Map(AnalysisKind.VocalPitch, $$"""{"kind": "vocal-pitch", "engine": "pyin", "frames": [{{frames}}], "source": "vocals"}""").ShouldBeNull();
    }

    [Theory]
    [InlineData(AnalysisKind.Chords, "drum-hits")]
    [InlineData(AnalysisKind.VocalPitch, "chords")]
    public void AResultOfAnotherKind_IsRefused(AnalysisKind kind, string wire) =>
        Map(kind, $$"""{"kind": "{{wire}}", "engine": "fake", "segments": [], "pieces": [], "hits": []}""").ShouldBeNull();
}
