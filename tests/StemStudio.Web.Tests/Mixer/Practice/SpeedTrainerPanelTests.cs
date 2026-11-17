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

/// <summary>The speed trainer panel (012 T052, FR-010, FR-016, FR-017; ui-test-ids 1.11.0 trainer-*).</summary>
public sealed class SpeedTrainerPanelTests : PracticeTestContext
{
    private static readonly SpeedTrainerSettingsDto Off = SpeedTrainerSettingsDto.Default;

    private void MixWithLoop(LoopRegionDto? loop, SpeedTrainerSettingsDto? trainer = null) =>
        Api.GetMixAsync(TestData.MixId, Arg.Any<CancellationToken>()).Returns(TestData.Mix() with
        {
            Settings = TestData.Settings() with { Loop = loop, Practice = PracticeSettingsDto.Default with { Trainer = trainer ?? Off } },
        });

    private IRenderedComponent<CascadingValue<ExperienceContext>> RenderWithLoop(
        LoopRegionDto? loop = null, SpeedTrainerSettingsDto? trainer = null, ExperienceLevel level = ExperienceLevel.Advanced)
    {
        MixWithLoop(loop ?? new LoopRegionDto(10, 14, true), trainer);
        var page = RenderAt(level, withMix: true);
        page.WaitForAssertion(() => page.Find("[data-testid=loop-toggle]").GetAttribute("aria-pressed").ShouldBe(loop?.Enabled == false ? "false" : "true"));
        return page;
    }

    private Task CommandAsync(IRenderedComponent<CascadingValue<ExperienceContext>> page, string command) =>
        page.InvokeAsync(() => Interop.CommandAsync(ShortcutScope.Mixer, command));

    private static AngleSharp.Dom.IElement Id(IRenderedComponent<CascadingValue<ExperienceContext>> page, string id) => page.Find($"[data-testid={id}]");

    [Fact]
    public void Panel_IsANamedRegion_WithItsHelpMarker_AtAdvanced()
    {
        var page = RenderWithLoop();

        var panel = Id(page, "trainer-panel");
        panel.GetAttribute("role").ShouldBe("group");
        page.Find($"#{panel.GetAttribute("aria-labelledby")}").TextContent.ShouldContain("Speed trainer");
        panel.GetAttribute("data-help-id").ShouldBe("practice-speed-trainer");
        panel.ParentElement!.Closest("[data-testid=practice-panel]").ShouldNotBeNull();
    }

    [Theory]
    [InlineData(ExperienceLevel.Advanced, true)]
    [InlineData(ExperienceLevel.Expert, true)]
    [InlineData(ExperienceLevel.Beginner, false)]
    public void Panel_IsAdvancedAndExpertOnly(ExperienceLevel level, bool shown)
    {
        var page = RenderWithLoop(level: level);

        foreach (var id in new[] { "trainer-panel", "trainer-toggle", "trainer-start", "trainer-step", "trainer-target", "trainer-passes", "trainer-status" })
        {
            page.FindAll($"[data-testid={id}]").Count.ShouldBe(shown ? 1 : 0, id);
        }
    }

    [Fact]
    public void WithoutALoop_TheToggleIsDisabled_AndSaysWhy()
    {
        var page = RenderAt();

        Id(page, "trainer-toggle").HasAttribute("disabled").ShouldBeTrue();
        Id(page, "trainer-needs-loop").TextContent.Trim().ShouldBe("Set a loop (A-B) to use the speed trainer.");
        Id(page, "trainer-toggle").GetAttribute("aria-describedby").ShouldBe(Id(page, "trainer-needs-loop").Id);
    }

    [Fact]
    public void WithATooShortLoop_TheToggleIsDisabled_WithTheHalfSecondRule()
    {
        var page = RenderWithLoop(new LoopRegionDto(10, 10.3, true));

        Id(page, "trainer-toggle").HasAttribute("disabled").ShouldBeTrue();
        Id(page, "trainer-needs-loop").TextContent.Trim().ShouldBe("The loop must be at least 0.5 s long.");
    }

    [Fact]
    public void WithAnEnabledLoop_TheToggleTurnsTheTrainerOn()
    {
        var page = RenderWithLoop();
        page.FindAll("[data-testid=trainer-needs-loop]").ShouldBeEmpty();
        var toggle = Id(page, "trainer-toggle");
        (toggle.GetAttribute("role"), toggle.GetAttribute("aria-checked")).ShouldBe(("switch", "false"));
        NextInterval();

        toggle.Click();

        Id(page, "trainer-toggle").GetAttribute("aria-checked").ShouldBe("true");
        Engine.Received().SetPracticeAsync(PracticeSettingsDto.Default with { Trainer = Off with { Enabled = true } });
        page.Find("[data-testid=unsaved-indicator]").ShouldNotBeNull();
    }

    [Fact]
    public void Unavailable_DisablesTheToggle()
    {
        var page = RenderWithLoop();

        RaiseStatus(PracticeStatus.ForUnavailable("No AudioWorklet."));

        page.WaitForAssertion(() => Id(page, "trainer-toggle").HasAttribute("disabled").ShouldBeTrue());
    }

    [Fact]
    public void AnEnabledTrainer_CanBeSwitchedOff_AfterItsLoopWasRemoved()
    {
        var page = RenderWithLoop(new LoopRegionDto(10, 14, false), Off with { Enabled = true });

        Id(page, "trainer-toggle").HasAttribute("disabled").ShouldBeFalse();
        Id(page, "trainer-toggle").Click();

        Id(page, "trainer-toggle").GetAttribute("aria-checked").ShouldBe("false");
    }

    [Fact]
    public void Inputs_ShowTheSettings_WithTheirRanges()
    {
        var page = RenderWithLoop();

        foreach (var (id, min, max, value) in new[] { ("trainer-start", "50", "119", "70"), ("trainer-step", "1", "20", "5"),
                     ("trainer-target", "51", "120", "100"), ("trainer-passes", "1", "8", "1") })
        {
            var input = Id(page, id);
            (input.GetAttribute("type"), input.GetAttribute("min"), input.GetAttribute("max"), input.GetAttribute("value")).ShouldBe(("number", min, max, value), id);
            page.Find($"label[for={input.Id}]").TextContent.ShouldNotBeNullOrWhiteSpace(id);
        }
    }

    [Fact]
    public void ValidEdits_AreSent_AndSaved()
    {
        var page = RenderWithLoop();
        NextInterval();

        Id(page, "trainer-start").Change("60");
        NextInterval();
        Id(page, "trainer-step").Change("10");
        NextInterval();
        Id(page, "trainer-target").Change("80");
        NextInterval();
        Id(page, "trainer-passes").Change("2");

        page.WaitForAssertion(() => Engine.Received().SetPracticeAsync(PracticeSettingsDto.Default with { Trainer = new SpeedTrainerSettingsDto(false, 60, 10, 80, 2) }));
        page.FindAll("[data-testid=trainer-error]").ShouldBeEmpty();
    }

    [Fact]
    public void TargetNotAboveStart_ShowsTheError_AndSendsNothing_UntilFixed()
    {
        var page = RenderWithLoop();
        NextInterval();
        Engine.ClearReceivedCalls();

        Id(page, "trainer-target").Change("60");

        Id(page, "trainer-error").TextContent.Trim().ShouldBe("The target must be above the start speed.");
        Id(page, "trainer-error").GetAttribute("role").ShouldBe("alert");
        Engine.DidNotReceive().SetPracticeAsync(Arg.Any<PracticeSettingsDto>());

        Id(page, "trainer-start").Change("55");

        page.FindAll("[data-testid=trainer-error]").ShouldBeEmpty();
        Engine.Received(1).SetPracticeAsync(PracticeSettingsDto.Default with { Trainer = Off with { StartPercent = 55, TargetPercent = 60 } });
    }

    [Theory]
    [InlineData("trainer-start", "49", "Start must be a whole number from 50 to 119 %.")]
    [InlineData("trainer-step", "25", "Step must be a whole number from 1 to 20 %.")]
    [InlineData("trainer-target", "121", "Target must be a whole number from 51 to 120 %.")]
    [InlineData("trainer-passes", "0", "Passes per step must be a whole number from 1 to 8.")]
    [InlineData("trainer-passes", "1.5", "Passes per step must be a whole number from 1 to 8.")]
    public void OutOfRangeEdits_ShowTheRange(string id, string value, string message)
    {
        var page = RenderWithLoop();

        Id(page, id).Change(value);

        Id(page, "trainer-error").TextContent.Trim().ShouldBe(message);
    }

    [Fact]
    public void Status_WhileRunning_ShowsPassAndSpeed_AndAnnouncesOnlyTheSpeed()
    {
        var page = RenderWithLoop(trainer: Off with { Enabled = true });

        RaiseStatus(Running(80, pass: 3));

        page.WaitForAssertion(() => Id(page, "trainer-status").TextContent.Trim().ShouldBe("Pass 3 - 80 % (target 100 %)"));
        var status = Id(page, "trainer-status");
        (status.GetAttribute("data-state"), status.GetAttribute("data-pass"), status.GetAttribute("data-speed")).ShouldBe(("running", "3", "80"));
        (status.GetAttribute("aria-live"), status.GetAttribute("aria-atomic")).ShouldBe(("polite", "true"));
        status.QuerySelector("[aria-hidden=true]")!.TextContent.ShouldBe("Pass 3 - ");
    }

    /// <summary>Engine trainer progress, the expected <c>data-state</c> and the expected text.</summary>
    public static TheoryData<TrainerStatus, string, string> States => new()
    {
        { new TrainerStatus(TrainerState.Off, 0, 100), "off", "Off" },
        { new TrainerStatus(TrainerState.Waiting, 0, 70), "waiting", "Ready - starts at 70 % (target 100 %)" },
        { new TrainerStatus(TrainerState.Target, 9, 100), "target", "Pass 9 - 100 % (target reached)" },
    };

    [Theory]
    [MemberData(nameof(States))]
    public void Status_ShowsEveryState(TrainerStatus trainer, string name, string text)
    {
        var page = RenderWithLoop(trainer: Off with { Enabled = true });

        RaiseStatus(PracticeStatus.Initial with { Stretch = StretchState.Active, Trainer = trainer });

        page.WaitForAssertion(() => Id(page, "trainer-status").GetAttribute("data-state").ShouldBe(name));
        Id(page, "trainer-status").TextContent.Trim().ShouldBe(text);
    }

    [Fact]
    public async Task TrainerKey_TogglesTheTrainer_WithALoop()
    {
        var page = RenderWithLoop();

        await CommandAsync(page, "mixer.trainer.toggle");

        Id(page, "trainer-toggle").GetAttribute("aria-checked").ShouldBe("true");
    }

    [Fact]
    public async Task TrainerKey_IsIgnored_WithoutALoop_AndBelowAdvanced()
    {
        var noLoop = RenderAt();
        await CommandAsync(noLoop, "mixer.trainer.toggle");
        Id(noLoop, "trainer-toggle").GetAttribute("aria-checked").ShouldBe("false");
        await DisposeComponentsAsync();

        var beginner = RenderWithLoop(level: ExperienceLevel.Beginner);
        NextInterval();
        Engine.ClearReceivedCalls();
        await CommandAsync(beginner, "mixer.trainer.toggle");

        await Engine.DidNotReceive().SetPracticeAsync(Arg.Any<PracticeSettingsDto>());
    }

    [Fact]
    public void HelpTopic_AndAdvancedTour_CoverTheTrainer()
    {
        var catalog = HelpCatalog.FromAssembly(typeof(HelpCatalog).Assembly);

        var topic = catalog.Topic("practice-speed-trainer").ShouldNotBeNull();
        topic.Reviewed.ShouldBe("012");
        topic.Feature.ShouldBe("practice.trainer");
        topic.Markdown.ShouldContain("0.5 s");
        var tour = catalog.Tours.Single(t => t.Id == "mixer-advanced");
        tour.Version.ShouldBe(2);
        tour.Steps.Select(s => (s.Target, s.Topic)).ShouldContain(("practice-panel", "practice-speed-trainer"));
        FeatureCatalog.IsVisible("practice.trainer", ExperienceLevel.Beginner).ShouldBeFalse();
        FeatureCatalog.IsVisible("practice.trainer", ExperienceLevel.Advanced).ShouldBeTrue();
    }
}
