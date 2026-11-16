using Bunit;
using NSubstitute;
using StemStudio.Contracts.Mixes;
using StemStudio.Web.Audio;
using StemStudio.Web.Experience;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Mixer.Practice;

/// <summary>The practice panel of the mixer (012 T037; ui-test-ids 1.11.0, FR-001, FR-002, FR-008, FR-009, FR-016).</summary>
public sealed class PracticePanelTests : PracticeTestContext
{
    /// <summary>The ids ui-test-ids 1.11.0 allows at Beginner that render without a count-in, a slow speed or an error.</summary>
    private static readonly string[] BeginnerAlways =
    [
        "practice-panel", "practice-speed", "practice-speed-value", "practice-speed-down", "practice-speed-up", "practice-transpose",
        "practice-transpose-down", "practice-transpose-up", "practice-reset", "practice-effective-bpm",
    ];

    private static readonly string[] AdvancedOrHigher = ["practice-speed-reset"];

    private static readonly string[] ExpertOnly = ["practice-speed-input", "practice-transpose-input", "practice-stretch-status"];

    [Fact]
    public void Panel_IsANamedGroup_InTheConsolePanels_WithTheEngineState()
    {
        var page = RenderAt();

        var panel = page.Find(".ss-console-panels [data-testid=practice-panel]");
        panel.GetAttribute("role").ShouldBe("group");
        page.Find($"#{panel.GetAttribute("aria-labelledby")}").TextContent.ShouldContain("Practice tempo");
        (panel.GetAttribute("data-stretch"), panel.GetAttribute("data-speed"), panel.GetAttribute("data-transpose")).ShouldBe(("bypass", "100", "0"));
    }

    [Fact]
    public void Panel_FollowsTheEngineStatus_OnTheRendererThread()
    {
        var page = RenderAt();

        RaiseStatus(PracticeStatus.Initial with { Stretch = StretchState.Loading });

        page.WaitForAssertion(() => page.Find("[data-testid=practice-panel]").GetAttribute("data-stretch").ShouldBe("loading"));
        page.Find("[data-testid=practice-panel]").GetAttribute("aria-busy").ShouldBe("true");
    }

    [Fact]
    public void Reset_IsDisabledAtNeutral_AndRestoresSpeedAndKey()
    {
        var page = RenderAt();
        var reset = page.Find("[data-testid=practice-reset]");
        reset.TextContent.ShouldEndWith("Original speed and key");
        reset.HasAttribute("disabled").ShouldBeTrue();

        page.Find("[data-testid=practice-speed]").Input("80");
        page.Find("[data-testid=practice-transpose-up]").Click();
        NextInterval();
        page.Find("[data-testid=practice-reset]").HasAttribute("disabled").ShouldBeFalse();
        page.Find("[data-testid=practice-reset]").Click();
        NextInterval();

        Engine.Received().SetPracticeAsync(PracticeSettingsDto.Default);
        page.Find("[data-testid=practice-panel]").GetAttribute("data-speed").ShouldBe("100");
        page.Find("[data-testid=practice-reset]").HasAttribute("disabled").ShouldBeTrue();
    }

    [Theory]
    [InlineData(100, "≈ 114 BPM")]
    [InlineData(80, "≈ 91 BPM")]
    public void EffectiveBpm_IsTheGridTempoTimesTheHeardSpeed(int speed, string text)
    {
        var page = RenderAt();

        page.Find("[data-testid=practice-speed]").Input(speed.ToString(System.Globalization.CultureInfo.InvariantCulture));

        page.Find("[data-testid=practice-effective-bpm]").TextContent.Trim().ShouldBe(text);
    }

    [Fact]
    public void EffectiveBpm_UsesTheManualBpm_AndIsHiddenWithoutATempo()
    {
        Api.GetTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(TestData.Track() with { BeatGrid = null });
        var page = RenderAt();
        page.FindAll("[data-testid=practice-effective-bpm]").ShouldBeEmpty();

        page.Find("[data-testid=metronome-bpm]").Change("120");
        page.Find("[data-testid=practice-speed]").Input("75");

        page.Find("[data-testid=practice-effective-bpm]").TextContent.Trim().ShouldBe("≈ 90 BPM");
    }

    [Theory]
    [InlineData(69, 0, true)]
    [InlineData(70, 0, false)]
    [InlineData(100, 8, true)]
    [InlineData(100, -7, false)]
    public void QualityHint_ShowsBelowSeventyPercent_OrBeyondSevenSemitones(int speed, int transpose, bool shown)
    {
        MixWith(At(speed, transpose));

        var page = RenderAt(withMix: true);

        page.WaitForAssertion(() => page.Find("[data-testid=practice-panel]").GetAttribute("data-speed").ShouldBe(speed.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        var hint = page.FindAll("[data-testid=practice-quality-hint]");
        hint.Count.ShouldBe(shown ? 1 : 0);
        if (shown)
        {
            hint[0].GetAttribute("data-help-id").ShouldBe("practice-sound-quality");
            hint[0].TextContent.ShouldContain("normal");
        }
    }

    [Fact]
    public void Unavailable_DisablesSpeedAndTranspose_WithTheReason_AndPlaybackStays()
    {
        var page = RenderAt();

        RaiseStatus(PracticeStatus.ForUnavailable("AudioWorklet is not supported in this browser."));

        page.WaitForAssertion(() => page.Find("[data-testid=practice-unavailable]").TextContent.Trim()
            .ShouldBe("AudioWorklet is not supported in this browser. Playback works at the original speed and key."));
        page.Find("[data-testid=practice-panel]").GetAttribute("data-stretch").ShouldBe("unavailable");
        page.Find("[data-testid=practice-speed]").HasAttribute("disabled").ShouldBeTrue();
        foreach (var id in new[] { "practice-speed-down", "practice-speed-up", "practice-transpose-down", "practice-transpose-up", "practice-reset" })
        {
            page.Find($"[data-testid={id}]").HasAttribute("disabled").ShouldBeTrue(id);
        }

        page.Find("[data-testid=practice-transpose]").GetAttribute("aria-disabled").ShouldBe("true");
        page.Find("[data-testid=transport-play]").HasAttribute("disabled").ShouldBeFalse();
    }

    [Fact]
    public void UnavailableAfterAChange_ShowsTheOriginalSpeedAsHeard()
    {
        var page = RenderAt();
        page.Find("[data-testid=practice-speed]").Input("70");

        RaiseStatus(PracticeStatus.ForUnavailable("The time-stretch module failed to load."));

        page.WaitForAssertion(() => page.Find("[data-testid=practice-panel]").GetAttribute("data-speed").ShouldBe("100"));
        page.Find("[data-testid=practice-speed-value]").TextContent.Trim().ShouldBe("100 %");
    }

    [Fact]
    public void InvalidSettings_RejectedByTheEngine_ShowTheEngineError()
    {
        Engine.SetPracticeAsync(Arg.Any<PracticeSettingsDto>()).Returns(Task.FromException(new AudioEngineException("Invalid practice settings: speedPercent out of range")));
        var page = RenderAt();

        page.Find("[data-testid=practice-speed]").Input("80");

        page.Find("[data-testid=mixer-engine-error]").TextContent.ShouldContain("Invalid practice settings");
    }

    [Fact]
    public void Expert_StretchStatus_ShowsLatencyAndDropouts()
    {
        var page = RenderAt(ExperienceLevel.Expert);

        RaiseStatus(PracticeStatus.Initial with { Stretch = StretchState.Active, LatencyMs = 106.4, Underruns = 2 });

        page.WaitForAssertion(() => page.Find("[data-testid=practice-stretch-status]").TextContent.Trim().ShouldBe("Stretch: active, latency 106 ms, 2 dropouts"));
        var status = page.Find("[data-testid=practice-stretch-status]");
        (status.GetAttribute("data-latency-ms"), status.GetAttribute("data-underruns")).ShouldBe(("106", "2"));
        status.GetAttribute("data-help-id").ShouldBe("practice-sound-quality");
    }

    [Fact]
    public void Beginner_RendersOnlyTheBeginnerIds()
    {
        MixWith(At(60, 9));
        var page = RenderAt(ExperienceLevel.Beginner, withMix: true);
        page.WaitForAssertion(() => page.Find("[data-testid=practice-quality-hint]").ShouldNotBeNull());

        foreach (var id in BeginnerAlways)
        {
            page.FindAll($"[data-testid={id}]").Count.ShouldBe(1, id);
        }

        foreach (var id in AdvancedOrHigher.Concat(ExpertOnly))
        {
            page.FindAll($"[data-testid={id}]").ShouldBeEmpty(id);
        }
    }

    [Fact]
    public void Advanced_AddsTheSpeedResetCountInOptionsAndTrainer_ButNoExpertIds()
    {
        var page = RenderAt();
        page.Find("[data-testid=practice-speed]").Input("90");

        foreach (var id in AdvancedOrHigher)
        {
            page.FindAll($"[data-testid={id}]").Count.ShouldBe(1, id);
        }

        foreach (var id in ExpertOnly)
        {
            page.FindAll($"[data-testid={id}]").ShouldBeEmpty(id);
        }
    }

    [Fact]
    public void Expert_AddsNumberEntryAndDiagnostics()
    {
        var page = RenderAt(ExperienceLevel.Expert);

        foreach (var id in ExpertOnly)
        {
            page.FindAll($"[data-testid={id}]").Count.ShouldBe(1, id);
        }
    }

    [Fact]
    public void NeutralMixer_DoesNotCallSetPractice_OnOpen()
    {
        RenderAt();

        Engine.DidNotReceive().SetPracticeAsync(Arg.Any<PracticeSettingsDto>());
    }

    [Fact]
    public void SavedPractice_IsSentOnOpen_AndShown()
    {
        MixWith(At(75, -2));

        var page = RenderAt(withMix: true);

        page.WaitForAssertion(() => Engine.Received(1).SetPracticeAsync(At(75, -2)));
        page.Find("[data-testid=practice-speed]").GetAttribute("value").ShouldBe("75");
        page.Find("[data-testid=practice-transpose]").TextContent.Trim().ShouldBe("-2");
    }

    [Fact]
    public void PracticeChange_MarksTheMixerUnsaved()
    {
        var page = RenderAt();

        page.Find("[data-testid=practice-transpose-down]").Click();

        page.Find("[data-testid=unsaved-indicator]").ShouldNotBeNull();
    }

    [Fact]
    public void PracticeStyles_UseDesignTokensOnly_AndAreLoaded()
    {
        var css = RepoPaths.ReadWeb("wwwroot/css/components/practice.css");

        css.ShouldNotBeEmpty();
        System.Text.RegularExpressions.Regex.IsMatch(css, @"#[0-9a-fA-F]{3,8}\b|rgba?\(|hsla?\(").ShouldBeFalse();
        RepoPaths.ReadWeb("Components/App.razor").ShouldContain("\"practice\"");
        css.ShouldContain("[data-motion=\"reduced\"]");
    }
}
