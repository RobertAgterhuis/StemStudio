using StemStudio.Domain.InstrumentViews;
using StemStudio.Domain.Scores;

namespace StemStudio.Domain.Tests.Scores;

/// <summary>Feature 016 T108: the section profiles and the part rules (data-model 4.2-4.4; FR-050..FR-053).</summary>
public sealed class ScorePartTests
{
    private static readonly Guid TrackId = Guid.Parse("0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a");
    private static readonly ScoreContext Cpu = new(262.0, DrumsSplit: true, GpuWorker: false);

    private static ScoreRequest Part(string channel, ScoreProfile profile, PartSettings part) =>
        new(ScoreSource.FromChannel(TrackId, channel)) { Profile = profile, Part = part };

    [Theory]
    [InlineData("guitar.strings", ScoreProfile.Strings)]
    [InlineData("piano.brass", ScoreProfile.Brass)]
    [InlineData("other.woodwinds", ScoreProfile.Woodwinds)]
    [InlineData("other.strings", ScoreProfile.Strings)]
    [InlineData("other.keys", ScoreProfile.Keys)]
    [InlineData("guitar.electric-guitar", ScoreProfile.Guitar)]
    public void Suggest_SectionStemsGetTheSectionProfiles(string channel, ScoreProfile profile) => ProfileCatalog.Suggest(channel).ShouldBe(profile);

    [Fact]
    public void Allowed_ASectionStemAddsItsProfileToThe008Profiles_WithinTheContractLimitOfSix()
    {
        ProfileCatalog.Allowed("other.brass").ShouldBe([ScoreProfile.Vocals, ScoreProfile.Bass, ScoreProfile.Guitar, ScoreProfile.Keys, ScoreProfile.Piano, ScoreProfile.Brass]);
        ProfileCatalog.Allowed("bass").Count.ShouldBe(5);
        ProfileCatalog.Allowed(null, ScoreProfile.Woodwinds).ShouldContain(ScoreProfile.Woodwinds);
        ProfileCatalog.Allowed(null, ScoreProfile.Bass).ShouldBe(ProfileCatalog.Pitched);
        ProfileCatalog.Allowed(null).ShouldBe(ProfileCatalog.Pitched);
    }

    [Theory]
    [InlineData(ScoreProfile.Strings, "Strings")]
    [InlineData(ScoreProfile.Brass, "Brass")]
    [InlineData(ScoreProfile.Woodwinds, "Woodwinds")]
    public void SectionProfiles_HaveANameALimitAndTheDefaults(ScoreProfile profile, string name)
    {
        (ProfileCatalog.InstrumentName(profile), ProfileCatalog.DefaultResolution(profile), ProfileCatalog.Engine(profile, null)).ShouldBe((name, ScoreResolution.Sixteenth, ScoreEngine.BasicPitch));
        ProfileCatalog.Limit(profile).Length.ShouldBeInRange(1, 500);
        ScoreNotation.ChordsAllowed(profile).ShouldBeTrue();
    }

    [Fact]
    public void ASectionProfile_ResolvesForItsStem_AndAPartIsKeptInTheRecipe()
    {
        var part = new PartSettings(WrittenInstrument.HornF, ClefChoice.Treble);

        var recipe = ScoreRecipeRules.Resolve(Part("other.brass", ScoreProfile.Brass, part), Cpu);

        (recipe.Profile, recipe.Part).ShouldBe((ScoreProfile.Brass, part));
        ScoreRecipeRules.Resolve(new ScoreRequest(ScoreSource.FromChannel(TrackId, "other.strings")), Cpu).Profile.ShouldBe(ScoreProfile.Strings);
        ScoreRecipeRules.Resolve(new ScoreRequest(ScoreSource.FromChannel(TrackId, "bass")), Cpu).Part.ShouldBeNull();
    }

    [Theory]
    [InlineData(ScoreProfile.Vocals)]
    [InlineData(ScoreProfile.Bass)]
    [InlineData(ScoreProfile.Guitar)]
    [InlineData(ScoreProfile.Keys)]
    [InlineData(ScoreProfile.Piano)]
    public void EveryPitchedProfile_TakesAnyWrittenInstrument(ScoreProfile profile) =>
        ScoreRecipeRules.Resolve(Part("guitar", profile, new PartSettings(WrittenInstrument.AltoSaxEb, ClefChoice.Auto)), Cpu).Part!.Instrument.ShouldBe(WrittenInstrument.AltoSaxEb);

    [Fact]
    public void Drums_TakeNoPart() =>
        Should.Throw<ScoreUnprocessableException>(() => ScoreRecipeRules.Resolve(Part("drums", ScoreProfile.Drums, new PartSettings(WrittenInstrument.Concert, ClefChoice.Auto)), Cpu))
            .Code.ShouldBe(ScoreUnprocessable.PartNotAllowed);

    [Theory]
    [InlineData(ScoreProfile.Keys, ClefChoice.Treble)]
    [InlineData(ScoreProfile.Piano, ClefChoice.Bass8vb)]
    public void GrandStaffProfiles_RefuseAClef(ScoreProfile profile, ClefChoice clef) =>
        Should.Throw<ScoreUnprocessableException>(() => ScoreRecipeRules.Resolve(Part("piano", profile, new PartSettings(WrittenInstrument.Concert, clef)), Cpu))
            .Code.ShouldBe(ScoreUnprocessable.ClefNotAllowed);

    [Fact]
    public void GrandStaffProfiles_TakeAnInstrumentWithTheAutomaticClef() =>
        ScoreRecipeRules.Resolve(Part("piano", ScoreProfile.Piano, new PartSettings(WrittenInstrument.Flute, ClefChoice.Auto)), Cpu).Part!.Clef.ShouldBe(ClefChoice.Auto);

    [Fact]
    public void PartRules_AreCheckedAfterTheTablatureRules() =>
        Should.Throw<ScoreUnprocessableException>(() => ScoreRecipeRules.Resolve(
            Part("piano", ScoreProfile.Keys, new PartSettings(WrittenInstrument.Concert, ClefChoice.Alto)) with { Tablature = new TablatureSettings(TuningId.GuitarStandard, 0, TabLayout.NotationAndTab) }, Cpu))
            .Code.ShouldBe(ScoreUnprocessable.TablatureNotAllowed);

    [Fact]
    public void ValuesOutsideTheCatalogue_AreFieldErrors_BeforeAnyRuleReadsThem()
    {
        var request = Part("guitar", ScoreProfile.Guitar, new PartSettings((WrittenInstrument)99, (ClefChoice)42)) with { Tablature = new TablatureSettings((TuningId)99, 0, (TabLayout)7) };

        var invalid = Should.Throw<DomainValidationException>(() => ScoreRecipeRules.Resolve(request, Cpu));

        invalid.Errors.Select(error => error.Field).ShouldBe(["tablature.tuning", "tablature.layout", "part.instrument", "part.clef"]);
    }
}
