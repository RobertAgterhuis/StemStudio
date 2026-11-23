using StemStudio.Domain.InstrumentViews;
using StemStudio.Domain.Scores;
using StemStudio.Domain.Tests.InstrumentViews;

namespace StemStudio.Domain.Tests.Scores;

/// <summary>Feature 016 T072: chord symbols in sheet music (FR-017): profiles that take them and the symbols of a window.</summary>
public sealed class ScoreNotationTests
{
    private static readonly ScoreContext Cpu = new(262.0, DrumsSplit: true, GpuWorker: false);

    [Theory]
    [InlineData(ScoreProfile.Vocals, true)]
    [InlineData(ScoreProfile.Guitar, true)]
    [InlineData(ScoreProfile.Keys, true)]
    [InlineData(ScoreProfile.Piano, true)]
    [InlineData(ScoreProfile.Strings, true)]
    [InlineData(ScoreProfile.Brass, true)]
    [InlineData(ScoreProfile.Woodwinds, true)]
    [InlineData(ScoreProfile.Bass, false)]
    [InlineData(ScoreProfile.Drums, false)]
    public void ChordsAllowed_FollowsTheProfile(ScoreProfile profile, bool allowed)
    {
        ScoreNotation.ChordsAllowed(profile).ShouldBe(allowed);
    }

    [Fact]
    public void Chords_KeepsTheWindow_ShiftsToItsStart_AndSkipsNoChord()
    {
        var chart = ChartData.Chart(("N", 1000), ("A:min7/b7", 2000), ("C:maj", 2000), ("G:7", 2000)) with { PreferFlats = true };

        var chords = ScoreNotation.Chords(chart, offsetSeconds: 2.0, endSeconds: 5.0);

        chords.PreferFlats.ShouldBeTrue();
        chords.Symbols.ShouldBe([new ScoreChord(0, 9, ChordQuality.Min7, 7), new ScoreChord(1000, 0, ChordQuality.Maj, null)]);
    }

    [Fact]
    public void Chords_WithoutAnEnd_RunToTheChartEnd_AndAreCapped()
    {
        var many = Enumerable.Range(0, ScoreNotation.MaxChordSymbols + 5).Select(_ => ("C:maj", 10)).ToArray();

        var chords = ScoreNotation.Chords(ChartData.Chart(many), offsetSeconds: 0, endSeconds: null);

        chords.Symbols.Count.ShouldBe(ScoreNotation.MaxChordSymbols);
        chords.Symbols[0].StartMs.ShouldBe(0);
        Should.Throw<ArgumentNullException>(() => ScoreNotation.Chords(null!, 0, null));
    }

    [Theory]
    [InlineData(ScoreProfile.Bass, "bass")]
    [InlineData(ScoreProfile.Drums, "drums")]
    public void Rules_RefuseIncludeChords_ForBassAndDrums(ScoreProfile profile, string channel)
    {
        var request = new ScoreRequest(new ScoreSource(Guid.NewGuid(), channel, null)) { Profile = profile, IncludeChords = true };

        var error = Should.Throw<DomainValidationException>(() => ScoreRecipeRules.Resolve(request, Cpu)).Errors.Single();

        (error.Field, error.Message).ShouldBe(("includeChords", ScoreNotation.ChordsNotAllowed));
    }

    [Fact]
    public void Rules_KeepIncludeChords_InTheRecipe()
    {
        var request = new ScoreRequest(new ScoreSource(Guid.NewGuid(), "guitar", null)) { IncludeChords = true };

        ScoreRecipeRules.Resolve(request, Cpu).IncludeChords.ShouldBeTrue();
    }
}
