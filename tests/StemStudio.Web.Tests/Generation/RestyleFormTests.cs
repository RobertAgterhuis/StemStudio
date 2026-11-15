using StemStudio.Contracts.Generation;
using StemStudio.Contracts.Styles;
using StemStudio.Web.Generation;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Generation;

/// <summary>Defaults, style changes and validation of the restyle dialog's form (009 FR-002, data-model 3.4).</summary>
public sealed class RestyleFormTests
{
    private static RestyleForm Form(double? duration = 213.04) =>
        new(TestData.Track() with { DurationSeconds = duration }, GenerationData.Styles());

    [Fact]
    public void Defaults_AreTheWholeTrack_AllStems_TheStyleStrength_AndOneTake()
    {
        var form = Form();

        (form.Style.Id, form.Mode, form.Start, form.End, form.StrengthIndex, form.Takes)
            .ShouldBe((GenerationData.JazzId, RestyleModeDto.Range, 0d, 213.04, 2, 1));
        form.IncludedStems.ShouldBe(["vocals", "drums", "bass", "guitar", "piano", "other"], ignoreOrder: true);
        form.Validate().ShouldBeNull();
    }

    [Fact]
    public void Defaults_StopAtEightMinutes_ForALongTrack()
    {
        var form = Form(600);

        form.End.ShouldBe(480);
        form.Validate().ShouldBeNull();
    }

    [Fact]
    public void AnInstrumentalStyle_DropsAndLocksTheVocals_AndSetsItsStrength()
    {
        var form = Form();

        form.SelectStyle(GenerationData.SynthwaveId);

        (form.VocalsLocked, form.StrengthIndex, form.StrengthName).ShouldBe((true, 3, "Strong"));
        form.IncludedStems.ShouldNotContain("vocals");
        form.SetStem("vocals", true);
        form.IncludedStems.ShouldNotContain("vocals");
    }

    [Fact]
    public void BackToAStyleWithVocals_UnlocksThem()
    {
        var form = Form();
        form.SelectStyle(GenerationData.SynthwaveId);

        form.SelectStyle(GenerationData.JazzId);
        form.SetStem("vocals", true);

        form.VocalsLocked.ShouldBeFalse();
        form.IncludedStems.ShouldContain("vocals");
    }

    [Fact]
    public void UnknownStyle_IsIgnored()
    {
        var form = Form();

        form.SelectStyle(Guid.NewGuid());

        form.Style.Id.ShouldBe(GenerationData.JazzId);
    }

    [Theory]
    [InlineData(0, 5, "The range must be 10 seconds to 8 minutes long.")]
    [InlineData(0, 500, "The range must be 10 seconds to 8 minutes long.")]
    [InlineData(-1, 20, "The range must lie inside the track (0:00 to 3:33).")]
    [InlineData(200, 214, "The range must lie inside the track (0:00 to 3:33).")]
    public void Validate_ChecksTheRange(double start, double end, string expected)
    {
        var form = Form();
        (form.Start, form.End) = (start, end);

        form.Validate().ShouldBe(expected);
    }

    [Fact]
    public void Validate_NeedsAStem_AndOneOrTwoTakes()
    {
        var form = Form();
        foreach (var stem in form.IncludedStems.ToList())
        {
            form.SetStem(stem, false);
        }

        form.Validate().ShouldBe("Choose at least one stem.");
        form.SetStem("drums", true);
        form.Takes = 3;
        form.Validate().ShouldBe("Choose 1 or 2 takes.");
    }

    [Fact]
    public void Inputs_FallBackWhenEmpty_AndClampTheStrength()
    {
        var form = Form();
        form.SetStart(12);
        form.SetStart(null);
        form.SetStrength(9);
        form.SetTakes(2);
        form.SetTakes(null);

        (form.Start, form.StrengthIndex, form.Takes, form.EndText).ShouldBe((0d, 4, 2, "213.04"));
        form.SetStrength(null);
        form.StrengthIndex.ShouldBe(4);
        Form(null).EndText.ShouldBe(string.Empty);
    }

    [Fact]
    public void WithoutADuration_TheRangeIsLeftToTheServer()
    {
        var form = Form(null);

        form.End.ShouldBeNull();
        form.Validate().ShouldBeNull();
        form.Request().RangeEndSeconds.ShouldBeNull();
        form.End = 20;
        form.Validate().ShouldBeNull();
    }

    [Fact]
    public void Request_CarriesTheChoices_InTrackStemOrder()
    {
        var form = Form();
        (form.Start, form.End, form.StrengthIndex, form.Takes) = (2, 14, 1, 2);
        form.SetStem("vocals", false);

        var request = form.Request();

        (request.StyleId, request.Mode, request.RangeStartSeconds, request.RangeEndSeconds, request.Strength, request.Takes)
            .ShouldBe((GenerationData.JazzId, RestyleModeDto.Range, 2d, 14d, StrengthDto.Light, 2));
        request.IncludedStems.ShouldBe(["drums", "bass", "guitar", "piano", "other"]);
    }
}
