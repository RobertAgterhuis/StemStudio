using StemStudio.Contracts.InstrumentViews;
using StemStudio.Web.InstrumentViews;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.InstrumentViews;

/// <summary>Harte labels of the edit dialog, the engine chart mapping and the status texts (T070, data-model 2.1, 9).</summary>
public sealed class ChordLabelsTests
{
    [Theory]
    [InlineData(9, ChordQualityDto.Min7, 7, "A:min7/b7")]
    [InlineData(0, ChordQualityDto.Maj, null, "C:maj")]
    [InlineData(0, ChordQualityDto.Maj, 0, "C:maj")]
    [InlineData(3, ChordQualityDto.Seventh, 7, "Eb:7/3")]
    [InlineData(10, ChordQualityDto.Hdim7, 2, "Bb:hdim7/3")]
    [InlineData(6, ChordQualityDto.Sus4, 5, "F#:sus4/7")]
    [InlineData(1, ChordQualityDto.Min, 2, "C#:min/b2")]
    [InlineData(11, ChordQualityDto.Dim, 1, "B:dim/2")]
    public void Harte_BuildsTheLabel_WithTheBassAsInterval(int root, ChordQualityDto quality, int? bass, string label) =>
        ChordLabels.Harte(root, quality, bass).ShouldBe(label);

    [Fact]
    public void Harte_WithoutRoot_IsNoChord() => ChordLabels.Harte(null, null, null).ShouldBe("N");

    [Fact]
    public void Harte_EveryBassInterval_MatchesTheContractPattern()
    {
        var pattern = new System.Text.RegularExpressions.Regex(
            "^(N|X|[A-G](#|b)?:(maj|min|dim|aug|min6|maj6|min7|minmaj7|maj7|7|dim7|hdim7|sus2|sus4)(/(b?[1-7]))?)$");

        foreach (var quality in Enum.GetValues<ChordQualityDto>())
        {
            for (var bass = 0; bass < 12; bass++)
            {
                pattern.IsMatch(ChordLabels.Harte(4, quality, bass)).ShouldBeTrue($"{quality} /{bass}");
            }
        }
    }

    [Fact]
    public void Qualities_PerVocabulary()
    {
        ChordLabels.Qualities(ChordVocabularyDto.Simple).ShouldBe([ChordQualityDto.Maj, ChordQualityDto.Min]);
        ChordLabels.Qualities(ChordVocabularyDto.Full).Count.ShouldBe(14);
        ChordLabels.QualityName(ChordQualityDto.Hdim7).ShouldBe("half-diminished 7 (m7b5)");
        ChordLabels.QualityName(ChordQualityDto.Maj).ShouldBe("major");
        ChordLabels.RootNames.Count.ShouldBe(12);
    }

    [Fact]
    public void ToEngine_MapsSegmentsKeyAndRegions()
    {
        var chart = ChordLabels.ToEngine(InstrumentViewsData.Chords().Document!);

        chart.Segments.Select(s => (s.StartMs, s.EndMs, s.Root, s.Quality, s.Bass)).ShouldBe([(0, 500, (int?)null, (string?)null, (int?)null), (500, 2500, 0, "maj", null)]);
        (chart.KeyTonic, chart.KeyMode).ShouldBe((0, "major"));
        chart.KeyRegions.ShouldBe([new StemStudio.Web.Audio.KeyRegionData(500, 16500, 0, "major")]);
    }

    [Fact]
    public void ToEngine_WithoutKey_HasNoTonic()
    {
        var chart = ChordLabels.ToEngine(InstrumentViewsData.Chords().Document! with { Key = null, KeyRegions = [] });

        (chart.KeyTonic, chart.KeyMode).ShouldBe((null, null));
        chart.KeyRegions.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(AnalysisStatusDto.None, "Chords have not been detected yet.")]
    [InlineData(AnalysisStatusDto.Queued, "Chord detection is waiting to start (about 12 s).")]
    [InlineData(AnalysisStatusDto.Running, "Detecting chords: 40 %.")]
    [InlineData(AnalysisStatusDto.Ready, "Chords are ready.")]
    [InlineData(AnalysisStatusDto.Failed, "Chord detection failed: The worker stopped.")]
    [InlineData(AnalysisStatusDto.Unavailable, "Chord detection is not available.")]
    [InlineData(AnalysisStatusDto.Stale, "The stems changed since the chords were detected; detect them again.")]
    public void Status_IsReadable(AnalysisStatusDto status, string text) =>
        AnalysisText.Status(InstrumentViewsData.Chords(status, 0.4), "Chord detection", "chords").ShouldBe(text);

    [Fact]
    public void Status_StaleWithReason_ShowsIt() =>
        AnalysisText.Status(InstrumentViewsData.StaleEditedChords(), "Chord detection", "chords")
            .ShouldBe("The stems changed since the chords were detected; detect them again. The drum stems were split again.");

    [Theory]
    [InlineData(AnalysisStatusDto.Ready, true)]
    [InlineData(AnalysisStatusDto.Stale, true)]
    [InlineData(AnalysisStatusDto.Running, false)]
    [InlineData(AnalysisStatusDto.Failed, false)]
    public void Shown_IsTheDocumentOfReadyAndStaleAnalyses(AnalysisStatusDto status, bool shown) =>
        (AnalysisText.Shown(InstrumentViewsData.Chords(status) with { Document = InstrumentViewsData.Chords().Document }) is not null).ShouldBe(shown);

    [Fact]
    public void Status_WithoutAnalysis_IsLoading() => AnalysisText.Status<ChordChartDocumentDto>(null, "Chord detection", "chords").ShouldBe("Loading chords...");

    [Fact]
    public void Status_UnavailableWithReason_ShowsIt() =>
        AnalysisText.Status(InstrumentViewsData.Chords(AnalysisStatusDto.Unavailable) with { Reason = "No harmonic stems." }, "Chord detection", "chords")
            .ShouldBe("Chord detection is not available: No harmonic stems.");
}
