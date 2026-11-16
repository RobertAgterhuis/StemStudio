using Bunit;
using NSubstitute;
using StemStudio.Contracts.Mixes;
using StemStudio.Web.Experience;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Mixer.Practice;

/// <summary>The speed slider, its buttons, the 100 % reset and the Expert number entry (012 T037, FR-001, FR-017).</summary>
public sealed class SpeedControlTests : PracticeTestContext
{
    [Fact]
    public void Slider_HasTheContractRangeKeysAndSpokenValue()
    {
        var page = RenderAt();

        var slider = page.Find("[data-testid=practice-speed]");
        (slider.GetAttribute("type"), slider.GetAttribute("min"), slider.GetAttribute("max"), slider.GetAttribute("step"))
            .ShouldBe(("range", "50", "120", "1"));
        (slider.GetAttribute("aria-valuenow"), slider.GetAttribute("aria-valuetext")).ShouldBe(("100", "100 percent"));
        slider.GetAttribute("data-page-step").ShouldBe("10");
        slider.GetAttribute("data-help-id").ShouldBe("practice-speed");
        slider.HasAttribute("aria-readonly").ShouldBeFalse();
        page.Find($"label[for={slider.Id}]").TextContent.ShouldContain("Speed");
        page.Find("[data-testid=practice-speed-value]").TextContent.Trim().ShouldBe("100 %");
    }

    [Fact]
    public void Dragging_SendsTheSpeed_AndShowsIt()
    {
        var page = RenderAt();

        page.Find("[data-testid=practice-speed]").Input("75");

        Engine.Received(1).SetPracticeAsync(At(75));
        var slider = page.Find("[data-testid=practice-speed]");
        (slider.GetAttribute("aria-valuenow"), slider.GetAttribute("aria-valuetext")).ShouldBe(("75", "75 percent"));
        page.Find("[data-testid=practice-speed-value]").TextContent.Trim().ShouldBe("75 %");
        page.Find("[data-testid=practice-panel]").GetAttribute("data-speed").ShouldBe("75");
    }

    [Fact]
    public void Dragging_IsCoalesced_ToOneEngineCallPer50Ms()
    {
        var page = RenderAt();

        foreach (var value in new[] { "95", "90", "85", "80" })
        {
            page.Find("[data-testid=practice-speed]").Input(value);
        }

        Engine.Received(1).SetPracticeAsync(Arg.Any<PracticeSettingsDto>());
        NextInterval();
        page.WaitForAssertion(() => Engine.Received(1).SetPracticeAsync(At(80)));
        Engine.Received(2).SetPracticeAsync(Arg.Any<PracticeSettingsDto>());
    }

    [Fact]
    public void Buttons_StepByFivePercent_AndClamp()
    {
        var page = RenderAt();

        page.Find("[data-testid=practice-speed-down]").Click();
        page.Find("[data-testid=practice-panel]").GetAttribute("data-speed").ShouldBe("95");
        page.Find("[data-testid=practice-speed]").Input("118");
        page.Find("[data-testid=practice-speed-up]").Click();

        page.Find("[data-testid=practice-panel]").GetAttribute("data-speed").ShouldBe("120");
        page.Find("[data-testid=practice-speed-down]").GetAttribute("aria-label").ShouldBe("Slower by 5 percent");
        page.Find("[data-testid=practice-speed-up]").GetAttribute("aria-label").ShouldBe("Faster by 5 percent");
    }

    [Fact]
    public void SpeedReset_ExistsOnlyAwayFrom100_AndRestores100()
    {
        var page = RenderAt();
        page.FindAll("[data-testid=practice-speed-reset]").ShouldBeEmpty();

        page.Find("[data-testid=practice-speed]").Input("85");
        page.Find("[data-testid=practice-transpose-up]").Click();
        page.Find("[data-testid=practice-speed-reset]").TextContent.Trim().ShouldBe("100 %");
        page.Find("[data-testid=practice-speed-reset]").Click();
        NextInterval();

        page.WaitForAssertion(() => Engine.Received().SetPracticeAsync(At(100, 1)));
        page.FindAll("[data-testid=practice-speed-reset]").ShouldBeEmpty();
    }

    [Fact]
    public void WhileTheTrainerRuns_TheSliderIsReadOnly_AndShowsTheTrainerSpeed()
    {
        var page = RenderAt();

        RaiseStatus(Running(65, pass: 2));

        page.WaitForAssertion(() => page.Find("[data-testid=practice-speed]").GetAttribute("aria-readonly").ShouldBe("true"));
        page.Find("[data-testid=practice-speed]").GetAttribute("value").ShouldBe("65");
        page.Find("[data-testid=practice-speed-value]").TextContent.Trim().ShouldBe("65 %");
        page.Find("[data-testid=practice-panel]").GetAttribute("data-speed").ShouldBe("65");
        page.Find("[data-testid=practice-speed-down]").HasAttribute("disabled").ShouldBeTrue();
        page.Find("[data-testid=practice-speed]").Input("90");
        Engine.DidNotReceive().SetPracticeAsync(Arg.Any<PracticeSettingsDto>());
    }

    [Fact]
    public void Expert_NumberEntry_MirrorsTheSlider()
    {
        var page = RenderAt(ExperienceLevel.Expert);
        var input = page.Find("[data-testid=practice-speed-input]");
        (input.GetAttribute("type"), input.GetAttribute("min"), input.GetAttribute("max"), input.GetAttribute("value")).ShouldBe(("number", "50", "120", "100"));

        input.Change("80");

        Engine.Received(1).SetPracticeAsync(At(80));
        page.Find("[data-testid=practice-speed]").GetAttribute("value").ShouldBe("80");
        page.Find("[data-testid=practice-speed-input]").GetAttribute("aria-label").ShouldBe("Speed in percent");
    }

    [Theory]
    [InlineData("200", "120")]
    [InlineData("12", "50")]
    public void Expert_NumberEntry_Clamps(string typed, string expected)
    {
        var page = RenderAt(ExperienceLevel.Expert);

        page.Find("[data-testid=practice-speed-input]").Change(typed);

        page.Find("[data-testid=practice-panel]").GetAttribute("data-speed").ShouldBe(expected);
    }

    [Fact]
    public void Expert_NumberEntry_IgnoresText()
    {
        var page = RenderAt(ExperienceLevel.Expert);

        page.Find("[data-testid=practice-speed-input]").Change("fast");

        Engine.DidNotReceive().SetPracticeAsync(Arg.Any<PracticeSettingsDto>());
    }

    [Fact]
    public void BeforeTheEngineIsReady_ChangesAreKept_AndSentOnceItLoads()
    {
        var loaded = new TaskCompletionSource<double>();
        Engine.LoadTrackAsync(Arg.Any<StemStudio.Contracts.Tracks.TrackDto>()).Returns(loaded.Task);
        var page = Render<Microsoft.AspNetCore.Components.CascadingValue<ExperienceContext>>(p => p
            .Add(c => c.Value, ExperienceContext.Default)
            .AddChildContent<StemStudio.Web.Components.Mixer.Mixer>(m => m.Add(x => x.Id, TestData.TrackId)));

        page.Find("[data-testid=practice-speed]").Input("70");
        Engine.DidNotReceive().SetPracticeAsync(Arg.Any<PracticeSettingsDto>());
        loaded.SetResult(213.04);

        page.WaitForAssertion(() => Engine.Received(1).SetPracticeAsync(At(70)));
    }
}
