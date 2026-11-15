using Bunit;
using StemStudio.Contracts.Generation;
using StemStudio.Web.Experience;
using StemStudio.Web.Generation;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Generation;

/// <summary>Restyle only a section, in place (009 T072, US5-1, FR-014; ui-test-ids 1.8.0 restyle-mode-*).</summary>
public sealed class RestyleInPlaceTests : GenerationTestContext
{
    [Fact]
    public void ModeRadios_DefaultToRange_AndExplainInPlace()
    {
        var page = RenderDialog();

        var range = page.Find("[data-testid=restyle-mode-range]");
        var inPlace = page.Find("[data-testid=restyle-mode-inplace]");
        (range.GetAttribute("type"), inPlace.GetAttribute("type")).ShouldBe(("radio", "radio"));
        (range.HasAttribute("checked"), inPlace.HasAttribute("checked")).ShouldBe((true, false));
        range.GetAttribute("name").ShouldBe(inPlace.GetAttribute("name"));
        page.Find("#restyle-mode-inplace-help").TextContent.ShouldContain("rest of the song stays as it is");
        inPlace.GetAttribute("aria-describedby").ShouldBe("restyle-mode-inplace-help");
    }

    [Fact]
    public async Task InPlace_IsSent_AndRangeCanBeChosenAgain()
    {
        var page = RenderDialog();

        await page.Find("[data-testid=restyle-mode-inplace]").ChangeAsync(new() { Value = "on" });
        page.Find("[data-testid=restyle-mode-inplace]").HasAttribute("checked").ShouldBeTrue();
        await page.Find("[data-testid=restyle-range-start]").ChangeAsync(new() { Value = "5" });
        await page.Find("[data-testid=restyle-range-end]").ChangeAsync(new() { Value = "15" });
        await page.Find("[data-testid=restyle-start]").ClickAsync(new());

        (SentRestyle().Mode, SentRestyle().RangeStartSeconds, SentRestyle().RangeEndSeconds).ShouldBe((RestyleModeDto.InPlace, 5d, 15d));

        var again = RenderDialog();
        await again.Find("[data-testid=restyle-mode-inplace]").ChangeAsync(new() { Value = "on" });
        await again.Find("[data-testid=restyle-mode-range]").ChangeAsync(new() { Value = "on" });
        again.Find("[data-testid=restyle-mode-range]").HasAttribute("checked").ShouldBeTrue();
    }

    [Fact]
    public void InPlace_NeedsATrackOfAtMostEightMinutes()
    {
        var form = new RestyleForm(TestData.Track() with { DurationSeconds = 600 }, GenerationData.Styles())
        {
            Mode = RestyleModeDto.InPlace,
            Start = 10,
            End = 40,
        };

        form.Validate().ShouldBe("Restyle in place works on tracks of at most 8 minutes.");
        form.Mode = RestyleModeDto.Range;
        form.Validate().ShouldBeNull();
    }

    [Fact]
    public void Beginner_DoesNotSeeTheModes()
    {
        var page = RenderDialogAt(ExperienceLevel.Beginner);

        page.FindAll("[data-testid=restyle-mode-range]").ShouldBeEmpty();
        page.FindAll("[data-testid=restyle-mode-inplace]").ShouldBeEmpty();
    }

    [Fact]
    public void TakeDetails_SayInPlace()
    {
        var generation = GenerationData.Generation() with { Restyle = GenerationData.Restyle(RestyleModeDto.InPlace) };

        var page = RenderList(generation);

        page.Find($"[data-testid=take-details-{GenerationData.GenerationId}-1]").TextContent.ShouldContain("0:30 to 1:15, in place");
    }
}
