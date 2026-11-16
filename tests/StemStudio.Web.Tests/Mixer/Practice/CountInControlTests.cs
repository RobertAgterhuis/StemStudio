using Bunit;
using Microsoft.AspNetCore.Components;
using NSubstitute;
using StemStudio.Contracts.Mixes;
using StemStudio.Web.Audio;
using StemStudio.Web.Experience;
using StemStudio.Web.Help;
using StemStudio.Web.Interop;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Mixer.Practice;

/// <summary>Count-in switch, bars, every pass and the unavailable reason (012 T059, FR-012, FR-013, FR-016, FR-017).</summary>
public sealed class CountInControlTests : PracticeTestContext
{
    private static AngleSharp.Dom.IElement Id(IRenderedComponent<CascadingValue<ExperienceContext>> page, string id) => page.Find($"[data-testid={id}]");

    private Task CommandAsync(IRenderedComponent<CascadingValue<ExperienceContext>> page, string command) =>
        page.InvokeAsync(() => Interop.CommandAsync(ShortcutScope.Mixer, command));

    private static PracticeSettingsDto CountIn(int bars, bool everyPass = false) =>
        PracticeSettingsDto.Default with { CountIn = new CountInSettingsDto(bars, everyPass) };

    [Fact]
    public void Toggle_IsASwitch_WithItsHelpMarker()
    {
        var page = RenderAt(ExperienceLevel.Beginner);

        var toggle = Id(page, "countin-toggle");
        (toggle.GetAttribute("role"), toggle.GetAttribute("aria-checked")).ShouldBe(("switch", "false"));
        toggle.TextContent.Trim().ShouldBe("Count-in");
        toggle.GetAttribute("data-help-id").ShouldBe("practice-count-in");
        toggle.Closest("[data-testid=practice-panel]").ShouldNotBeNull();
    }

    [Fact]
    public void Beginner_TurnsOnOneBar()
    {
        var page = RenderAt(ExperienceLevel.Beginner);

        Id(page, "countin-toggle").Click();

        Id(page, "countin-toggle").GetAttribute("aria-checked").ShouldBe("true");
        Engine.Received(1).SetPracticeAsync(CountIn(1));
        page.FindAll("[data-testid=countin-bars]").ShouldBeEmpty();
        page.FindAll("[data-testid=countin-every-pass]").ShouldBeEmpty();
    }

    [Fact]
    public void Advanced_RestoresTheLastBarCount()
    {
        var page = RenderAt();

        Id(page, "countin-bars").Change("2");
        NextInterval();
        Id(page, "countin-toggle").Click();
        NextInterval();
        Id(page, "countin-toggle").Click();

        Engine.Received(2).SetPracticeAsync(CountIn(2));
        Id(page, "countin-toggle").GetAttribute("aria-checked").ShouldBe("true");
        Id(page, "countin-bars").GetAttribute("value").ShouldBe("2");
    }

    [Fact]
    public void Bars_OffersOneAndTwoBars()
    {
        var page = RenderAt();

        var select = Id(page, "countin-bars");
        select.TagName.ShouldBe("SELECT");
        select.QuerySelectorAll("option").Select(o => o.GetAttribute("value")).ShouldBe(["1", "2"]);
        select.GetAttribute("aria-label").ShouldBe("Count-in bars");
    }

    [Fact]
    public void EveryPass_IsDisabledWithoutALoop_AndPointsToTheReason()
    {
        var page = RenderAt();

        var box = Id(page, "countin-every-pass");
        box.GetAttribute("type").ShouldBe("checkbox");
        box.HasAttribute("disabled").ShouldBeTrue();
        box.GetAttribute("aria-describedby").ShouldBe(Id(page, "trainer-needs-loop").Id);
        page.Find($"label[for={box.Id}]").TextContent.Trim().ShouldBe("Before every loop pass");
    }

    [Fact]
    public void EveryPass_WithALoop_IsSent()
    {
        MixWith(CountIn(1));
        var page = RenderAt(withMix: true);
        page.WaitForAssertion(() => Id(page, "countin-toggle").GetAttribute("aria-checked").ShouldBe("true"));
        NextInterval();

        Id(page, "countin-every-pass").Change(true);

        Engine.Received(1).SetPracticeAsync(CountIn(1, everyPass: true));
        Id(page, "countin-every-pass").HasAttribute("checked").ShouldBeTrue();
    }

    [Fact]
    public void WithoutATempo_TheCountInIsUnavailable_WithTheReason()
    {
        Api.GetTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(TestData.Track() with { BeatGrid = TestData.Grid(reliable: false) });
        var page = RenderAt(ExperienceLevel.Beginner);

        Id(page, "countin-toggle").HasAttribute("disabled").ShouldBeTrue();
        Id(page, "countin-unavailable").TextContent.Trim().ShouldBe("Count-in needs a beat grid or a manual BPM.");
        Id(page, "countin-toggle").GetAttribute("aria-describedby").ShouldBe(Id(page, "countin-unavailable").Id);
    }

    [Fact]
    public void AManualBpm_MakesTheCountInAvailable()
    {
        Api.GetTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(TestData.Track() with { BeatGrid = null });
        var page = RenderAt();

        Id(page, "metronome-bpm").Change("96");

        page.FindAll("[data-testid=countin-unavailable]").ShouldBeEmpty();
        Id(page, "countin-toggle").HasAttribute("disabled").ShouldBeFalse();
    }

    [Fact]
    public void CountIn_StaysUsable_WhileTheStretchStageIsUnavailable()
    {
        var page = RenderAt();

        RaiseStatus(PracticeStatus.ForUnavailable("No AudioWorklet."));

        page.WaitForAssertion(() => page.Find("[data-testid=practice-unavailable]").ShouldNotBeNull());
        Id(page, "countin-toggle").HasAttribute("disabled").ShouldBeFalse();
    }

    [Fact]
    public async Task CountInKey_TogglesLikeTheSwitch()
    {
        var page = RenderAt(ExperienceLevel.Beginner);

        await CommandAsync(page, "mixer.countin.toggle");
        Id(page, "countin-toggle").GetAttribute("aria-checked").ShouldBe("true");
        await CommandAsync(page, "mixer.countin.toggle");

        Id(page, "countin-toggle").GetAttribute("aria-checked").ShouldBe("false");
    }

    [Fact]
    public async Task CountInKey_IsIgnored_WithoutATempo()
    {
        Api.GetTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(TestData.Track() with { BeatGrid = null });
        var page = RenderAt();

        await CommandAsync(page, "mixer.countin.toggle");

        Id(page, "countin-toggle").GetAttribute("aria-checked").ShouldBe("false");
        await Engine.DidNotReceive().SetPracticeAsync(Arg.Any<PracticeSettingsDto>());
    }

    [Fact]
    public void HelpTopics_AndCatalogue_CoverTheCountIn()
    {
        var catalog = HelpCatalog.FromAssembly(typeof(HelpCatalog).Assembly);

        catalog.Topic("practice-count-in").ShouldNotBeNull().Reviewed.ShouldBe("012");
        catalog.Topic("count-in").ShouldNotBeNull().Area.ShouldBe("glossary");
        catalog.Topic("mixer-metronome")!.Related.ShouldContain("practice-count-in");
        catalog.TopicForAnchor("practice-count-in")!.Id.ShouldBe("practice-count-in");
        FeatureCatalog.IsVisible("practice.count-in.options", ExperienceLevel.Beginner).ShouldBeFalse();
        FeatureCatalog.IsVisible("practice.count-in.options", ExperienceLevel.Advanced).ShouldBeTrue();
    }
}
