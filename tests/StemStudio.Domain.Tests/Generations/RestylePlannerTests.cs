using StemStudio.Domain.Generations;
using StemStudio.Domain.Styles;
using StemStudio.Domain.Tests.Styles;

namespace StemStudio.Domain.Tests.Generations;

/// <summary>009 T037: restyle request rules (backend RestyleRequest description, FR-001, FR-014).</summary>
public sealed class RestylePlannerTests
{
    private static readonly string[] Six = ["vocals", "drums", "bass", "guitar", "piano", "other"];

    [Fact]
    public void Plan_DefaultsToTheStartOfTheTrackAndAllStems()
    {
        var plan = RestylePlanner.Plan(new RestyleOptions(RestyleMode.Range, null, null, null, null, 1), Source(213.04), StyleTestData.JazzTrio());

        plan.Range.ShouldBe(new SourceRange(0, 213.04, RestyleMode.Range));
        plan.Stems.ShouldBe(Six);
        plan.Strength.ShouldBe(Strength.Balanced);
        plan.Takes.ShouldBe(1);
    }

    [Fact]
    public void Plan_EndDefaultsToAtMost480Seconds()
    {
        var plan = RestylePlanner.Plan(new RestyleOptions(RestyleMode.Range, 100, null, null, null, 2), Source(900), StyleTestData.JazzTrio());

        plan.Range.ShouldBe(new SourceRange(100, 580, RestyleMode.Range));
        plan.Takes.ShouldBe(2);
    }

    [Fact]
    public void Plan_InstrumentalStylesDropTheVocalsByDefault()
    {
        var plan = RestylePlanner.Plan(Options(), Source(213.04), StyleTestData.RainyCafe());

        plan.Stems.ShouldBe(["drums", "bass", "guitar", "piano", "other"]);
        plan.Strength.ShouldBe(Strength.Light);
    }

    [Fact]
    public void Plan_UsesExplicitStemsInCatalogOrderAndStrength()
    {
        var options = Options() with { IncludedStems = ["piano", "drums"], Strength = Strength.Free };

        var plan = RestylePlanner.Plan(options, Source(213.04), StyleTestData.JazzTrio());

        plan.Stems.ShouldBe(["drums", "piano"]);
        plan.Strength.ShouldBe(Strength.Free);
    }

    [Fact]
    public void Plan_InPlaceKeepsTheRange()
    {
        var plan = RestylePlanner.Plan(Options() with { Mode = RestyleMode.InPlace, Start = 15, End = 30 }, Source(213.04), StyleTestData.JazzTrio());

        plan.Range.ShouldBe(new SourceRange(15, 30, RestyleMode.InPlace));
    }

    [Theory]
    [InlineData(60.0, 65.0, "rangeEndSeconds")]
    [InlineData(0.0, 500.0, "rangeEndSeconds")]
    [InlineData(200.0, 220.0, "rangeEndSeconds")]
    [InlineData(-1.0, 30.0, "rangeStartSeconds")]
    [InlineData(210.0, null, "rangeEndSeconds")]
    public void Plan_RejectsBadRanges(double? start, double? end, string field)
    {
        var error = Should.Throw<DomainValidationException>(() =>
            RestylePlanner.Plan(Options() with { Start = start, End = end }, Source(213.04), StyleTestData.JazzTrio()));

        error.Errors.ShouldContain(e => e.Field == field);
    }

    [Fact]
    public void Plan_InPlaceNeedsATrackOfAtMost480Seconds()
    {
        var error = Should.Throw<DomainValidationException>(() =>
            RestylePlanner.Plan(Options() with { Mode = RestyleMode.InPlace, Start = 0, End = 60 }, Source(600), StyleTestData.JazzTrio()));

        error.Errors.Single().Field.ShouldBe("mode");
    }

    [Theory]
    [InlineData("")]
    [InlineData("kazoo")]
    [InlineData("drums,drums")]
    public void Plan_RejectsBadStems(string stems)
    {
        var list = stems.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var error = Should.Throw<DomainValidationException>(() =>
            RestylePlanner.Plan(Options() with { IncludedStems = list }, new RestyleSource(213.04, ["vocals", "drums", "bass", "other"]), StyleTestData.JazzTrio()));

        error.Errors.Single().Field.ShouldBe("includedStems");
    }

    [Fact]
    public void Plan_RejectsAStemTheTrackDoesNotHave() =>
        Should.Throw<DomainValidationException>(() =>
            RestylePlanner.Plan(Options() with { IncludedStems = ["piano"] }, new RestyleSource(213.04, ["vocals", "drums", "bass", "other"]), StyleTestData.JazzTrio()))
            .Errors.Single().Field.ShouldBe("includedStems");

    [Fact]
    public void Plan_AnInstrumentalStyleOnAVocalOnlyTrackHasNoStems() =>
        Should.Throw<DomainValidationException>(() =>
            RestylePlanner.Plan(Options() with { Start = 0, End = 30 }, new RestyleSource(60, ["vocals"]), StyleTestData.RainyCafe()))
            .Errors.Single().Field.ShouldBe("includedStems");

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void Plan_RejectsBadTakeCounts(int takes) =>
        Should.Throw<DomainValidationException>(() => RestylePlanner.Plan(Options() with { Takes = takes }, Source(213.04), StyleTestData.JazzTrio()))
            .Errors.Single().Field.ShouldBe("takes");

    [Fact]
    public void Plan_ATrackShorterThanTenSecondsCannotBeRestyled() =>
        Should.Throw<DomainValidationException>(() => RestylePlanner.Plan(Options(), Source(8), StyleTestData.JazzTrio()))
            .Errors.ShouldContain(e => e.Field == "rangeEndSeconds");

    private static RestyleOptions Options() => new(RestyleMode.Range, 60, 105, null, null, 1);

    private static RestyleSource Source(double duration) => new(duration, Six);
}
