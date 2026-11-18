using System.Text.Json;
using StemStudio.Domain.InstrumentViews;
using StemStudio.Domain.Scores;

namespace StemStudio.Domain.Tests.InstrumentViews;

/// <summary>Feature 016 T043 (S12): <see cref="CapoAdvisor"/> against the <c>capoFixtures</c> vectors and research R-14.</summary>
public sealed class CapoAdvisorTests
{
    public static TheoryData<int> Fixtures() => SpellingVectors.Indexes("capoFixtures");

    [Fact]
    public void Fixtures_AreFourProgressionsAtThreeTransposes() => Fixtures().Count.ShouldBe(12);

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void EveryFixture_GivesTheExpectedAdvice(int index)
    {
        var vector = SpellingVectors.At("capoFixtures", index);
        var chords = vector.GetProperty("segments").EnumerateArray()
            .Select(s => new TimedChord(s.GetProperty("root").GetInt32(), SpellingVectors.Quality(s)!.Value, s.GetProperty("durationMs").GetInt32())).ToList();
        var key = new MusicalKey(vector.GetProperty("keyTonic").GetInt32(), SpellingVectors.Mode(vector.GetProperty("keyMode").GetString()!));

        var advice = CapoAdvisor.Advise(chords, key, vector.GetProperty("transpose").GetInt32());

        var shortcut = vector.GetProperty("shortcut");
        advice.Shortcut.ShouldBe(shortcut.ValueKind == JsonValueKind.Null ? null : shortcut.GetProperty("capo").GetInt32());
        var expected = vector.GetProperty("suggestions").EnumerateArray()
            .Select(s => new CapoSuggestion(s.GetProperty("capo").GetInt32(), s.GetProperty("shapeKey").GetString()!, s.GetProperty("openShare").GetDouble(), s.GetProperty("fewOpenShapes").GetBoolean()))
            .ToList();
        advice.Suggestions.ShouldBe(expected);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(7, 7)]
    [InlineData(8, null)]
    [InlineData(0, null)]
    [InlineData(-1, null)]
    [InlineData(-5, 7)]
    [InlineData(-6, null)]
    [InlineData(-12, null)]
    public void Shortcut_KeepsTheOriginalShapes(int transpose, int? capo) => CapoAdvisor.Shortcut(transpose).ShouldBe(capo);

    [Fact]
    public void Ranking_PrefersLowerFretsOnNearTies()
    {
        // C G Am: all open at capo 0; at capo 5 the shapes are G D Em, also all open: 1.0 beats 1.0 - 0.1.
        var chords = new[] { new TimedChord(0, ChordQuality.Maj, 1000), new TimedChord(7, ChordQuality.Maj, 1000), new TimedChord(9, ChordQuality.Min, 1000) };

        var advice = CapoAdvisor.Advise(chords, new MusicalKey(0, KeyMode.Major), 0);

        advice.Suggestions[0].ShouldBe(new CapoSuggestion(0, "C major", 1, false));
        advice.Suggestions.Count.ShouldBe(3);
    }

    [Fact]
    public void BarreOnlyProgression_SaysFewOpenShapes()
    {
        var chords = new[] { new TimedChord(6, ChordQuality.Dim, 1000), new TimedChord(1, ChordQuality.Aug, 1000) };

        CapoAdvisor.Advise(chords, new MusicalKey(6, KeyMode.Major), 0).Suggestions.ShouldAllBe(s => s.FewOpenShapes && s.OpenShare == 0);
    }

    [Fact]
    public void EmptyProgression_HasShareZero() =>
        CapoAdvisor.Advise([], new MusicalKey(0, KeyMode.Major), 0).Suggestions.ShouldAllBe(s => s.OpenShare == 0);

    [Fact]
    public void Advise_RejectsNull() => Should.Throw<ArgumentNullException>(() => CapoAdvisor.Advise(null!, default, 0));

    [Fact]
    public void IsOpenShape_IgnoresQualitiesWithoutOpenShapes() =>
        CapoAdvisor.IsOpenShape(new TimedChord(0, ChordQuality.Min6, 1), ChordDisplay.Sounding).ShouldBeFalse();

    [Fact]
    public void IsOpenShape_RejectsNull() => Should.Throw<ArgumentNullException>(() => CapoAdvisor.IsOpenShape(null!, ChordDisplay.Sounding));
}
