using Bunit;
using Microsoft.AspNetCore.Components;
using NSubstitute;
using StemStudio.Contracts.InstrumentViews;
using StemStudio.Contracts.Recording;
using StemStudio.Web.Audio;
using StemStudio.Web.Components.InstrumentViews;
using StemStudio.Web.Experience;
using StemStudio.Web.InstrumentViews;
using StemStudio.Web.Recording;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.InstrumentViews;

/// <summary>The singing feedback panel (T105, FR-041..FR-047, FR-061, FR-062; ui-test-ids 1.15.0 singing-*).</summary>
public sealed class SingingPanelTests : PageTestContext
{
    private static readonly Guid Track = TestData.TrackId;

    private readonly List<bool> _running = [];
    private readonly List<ScoredUnit?> _scores = [];
    private int _settingsOpened;
    private LatencyInUse? _latency = new(1852, LatencyMethodDto.Loopback, Guid.NewGuid());

    public SingingPanelTests()
    {
        Api.GetLyricsAsync(Track, Arg.Any<CancellationToken>()).Returns(LyricsData.Found());
        Pitch(InstrumentViewsData.VocalPitch());
    }

    private void Pitch(params TrackAnalysisDto<VocalPitchDocumentDto>[] answers) =>
        Api.GetVocalPitchAsync(Track, Arg.Any<CancellationToken>()).Returns(answers[0], answers[1..]);

    private void Parameters(ComponentParameterCollectionBuilder<SingingPanel> p) => p
        .Add(c => c.Track, TestData.Track())
        .Add(c => c.Duration, 213)
        .Add(c => c.Sections, PracticeFlowData.Sections())
        .Add(c => c.EngineReady, true)
        .Add(c => c.Latency, _latency)
        .Add(c => c.OnRunningChanged, running => _running.Add(running))
        .Add(c => c.OnUnitScored, score => _scores.Add(score))
        .Add(c => c.OnOpenInputSettings, () => _settingsOpened++);

    private IRenderedComponent<SingingPanel> RenderPanel() => Render<SingingPanel>(Parameters);

    private IRenderedComponent<CascadingValue<ExperienceContext>> RenderAt(ExperienceLevel level) => RenderAtLevel<SingingPanel>(level, Parameters);

    private SingingFeedbackRequest SentRequest() =>
        (SingingFeedbackRequest)Views.ReceivedCalls().Single(c => c.GetMethodInfo().Name == nameof(Views.StartSingingFeedbackAsync)).GetArguments()[0]!;

    private void State(string state, string? reason = null) =>
        Views.SingingStateChanged += Raise.EventWith(Views, new SingingStateChange(state, reason));

    [Fact]
    public void Ready_ShowsTheRegion_TheEmptyPitchHost_TheHint_AndAnIdleState()
    {
        var panel = RenderPanel();

        panel.Find("[data-testid=singing]").GetAttribute("aria-label").ShouldBe("Singing feedback");
        panel.Find("[data-testid=singing-reference-status]").GetAttribute("data-status").ShouldBe("ready");
        var view = panel.Find("[data-testid=pitch-view]");
        (view.ChildElementCount, view.GetAttribute("role")).ShouldBe((0, "img"));
        panel.Find("[data-testid=singing-headphones-hint]").TextContent.ShouldContain("headphones");
        panel.Find("[data-testid=singing-state]").GetAttribute("data-state").ShouldBe("idle");
        var toggle = panel.Find("[data-testid=singing-toggle]");
        (toggle.GetAttribute("aria-pressed"), toggle.HasAttribute("disabled")).ShouldBe(("false", false));
        panel.Find(".ss-singing-limits").TextContent.ShouldContain("bleed");
    }

    [Fact]
    public void NoReferenceYet_IsAnalysedOnFirstOpen_WhenItIsQuick()
    {
        Pitch(InstrumentViewsData.VocalPitch(AnalysisStatusDto.None) with { EstimateSeconds = 30 });
        Api.AnalyseVocalPitchAsync(Track, Arg.Any<CancellationToken>()).Returns(InstrumentViewsData.VocalPitch(AnalysisStatusDto.Queued));

        var panel = RenderPanel();

        Api.Received(1).AnalyseVocalPitchAsync(Track, Arg.Any<CancellationToken>());
        panel.Find("[data-testid=singing-reference-status]").GetAttribute("data-status").ShouldBe("queued");
        var toggle = panel.Find("[data-testid=singing-toggle]");
        toggle.HasAttribute("disabled").ShouldBeTrue();
        toggle.GetAttribute("aria-describedby").ShouldBe("singing-reference-status");
    }

    [Fact]
    public async Task ASlowAnalysis_IsOfferedWithItsEstimate_NotStarted()
    {
        Pitch(InstrumentViewsData.VocalPitch(AnalysisStatusDto.None) with { EstimateSeconds = 95 }, InstrumentViewsData.VocalPitch());
        Api.AnalyseVocalPitchAsync(Track, Arg.Any<CancellationToken>()).Returns(InstrumentViewsData.VocalPitch(AnalysisStatusDto.Running));
        var panel = RenderPanel();

        _ = Api.DidNotReceive().AnalyseVocalPitchAsync(Track, Arg.Any<CancellationToken>());
        panel.Find("[data-testid=singing-reference-estimate]").TextContent.ShouldContain("95 s");
        await panel.Find("[data-testid=singing-reference-analyse]").ClickAsync(new());

        await Api.Received(1).AnalyseVocalPitchAsync(Track, Arg.Any<CancellationToken>());
        Timer.Tick();
        panel.WaitForAssertion(() => panel.Find("[data-testid=singing-reference-status]").GetAttribute("data-status").ShouldBe("ready"));
    }

    [Fact]
    public async Task Start_MutesTheLead_SendsTheRequest_AndStopShowsTheSummary()
    {
        Views.StopSingingFeedbackAsync().Returns(new SingingSummary { UnitsScored = 2, Average = 81, Best = new(1, 90), ToleranceCents = 100, LatencySource = "loopback" });
        var panel = RenderPanel();

        await panel.Find("[data-testid=singing-toggle]").ClickAsync(new());

        _running.ShouldBe([true]);
        var request = SentRequest();
        request.PitchViewElementId.ShouldBe(panel.Find("[data-testid=pitch-view]").Id);
        request.Reference.Frames.ShouldBe([0, 4500, 4510, 4700]);
        request.Units.Count.ShouldBe(2);
        (request.ToleranceCents, request.RoundTripMs, request.LatencyMethod).ShouldBe((100, 42.0, "loopback"));
        panel.Find("[data-testid=singing-toggle]").GetAttribute("aria-pressed").ShouldBe("true");

        await panel.Find("[data-testid=singing-toggle]").ClickAsync(new());

        _running.ShouldBe([true, false]);
        var summary = panel.Find("[data-testid=singing-summary]");
        (summary.GetAttribute("data-average"), summary.GetAttribute("data-units")).ShouldBe(("81", "2"));
        summary.TextContent.ShouldContain("Best: line 2 (90)");
    }

    [Fact]
    public async Task AnEstimatedLatency_LetsTheEngineUseItsOwn()
    {
        _latency = new LatencyInUse(2205, LatencyMethodDto.Estimated, null);
        var panel = RenderPanel();

        await panel.Find("[data-testid=singing-toggle]").ClickAsync(new());

        (SentRequest().RoundTripMs, SentRequest().LatencyMethod).ShouldBe((null, null));
    }

    [Fact]
    public async Task UnitScores_AreAnnounced_AndReported()
    {
        var panel = RenderPanel();
        await panel.Find("[data-testid=singing-toggle]").ClickAsync(new());

        Views.SingingUnitScored += Raise.EventWith(Views, new SingingUnitScore(1, 87, 1));

        panel.WaitForAssertion(() => panel.Find("[data-testid=singing-announcer]").TextContent.ShouldBe("Line 2: 87 points"));
        panel.Find("[data-testid=singing-announcer]").GetAttribute("aria-live").ShouldBe("polite");
        _scores.ShouldBe([null, new ScoredUnit("Line", new SingingUnitScore(1, 87, 1))]);
    }

    [Fact]
    public async Task NoInputDevice_IsExplained_WithTheWayToTheInputSettings()
    {
        Views.StartSingingFeedbackAsync(Arg.Any<SingingFeedbackRequest>()).Returns(Task.FromException(new AudioEngineException("No input device is open")));
        var panel = RenderPanel();

        await panel.Find("[data-testid=singing-toggle]").ClickAsync(new());

        var error = panel.Find("[data-testid=singing-error]");
        error.TextContent.ShouldContain("microphone");
        _running.ShouldBe([true, false]);
        panel.Find("[data-testid=singing-toggle]").GetAttribute("aria-pressed").ShouldBe("false");
        await error.QuerySelector("button")!.ClickAsync(new());
        _settingsOpened.ShouldBe(1);
    }

    [Fact]
    public async Task TooSlow_StopsWithTheReason_AndTheSummary()
    {
        Views.StopSingingFeedbackAsync().Returns(new SingingSummary { UnitsScored = 0 });
        var panel = RenderPanel();
        await panel.Find("[data-testid=singing-toggle]").ClickAsync(new());

        State("failed", "Pitch tracking is too slow on this device.");

        panel.WaitForAssertion(() => panel.Find("[data-testid=singing-error]").TextContent.ShouldContain("too slow"));
        panel.Find("[data-testid=singing-state]").GetAttribute("data-state").ShouldBe("failed");
        _running.ShouldBe([true, false]);
        panel.Find("[data-testid=singing-summary]").TextContent.ShouldContain("No line was scored");
    }

    [Fact]
    public async Task StateChanges_AreShown()
    {
        var panel = RenderPanel();
        await panel.Find("[data-testid=singing-toggle]").ClickAsync(new());

        State("running");

        panel.WaitForAssertion(() => panel.Find("[data-testid=singing-state]").GetAttribute("data-state").ShouldBe("running"));
        panel.Find("[data-testid=singing-state]").TextContent.ShouldContain("Listening");
    }

    [Theory]
    [InlineData(ExperienceLevel.Beginner, 100)]
    [InlineData(ExperienceLevel.Advanced, 50)]
    [InlineData(ExperienceLevel.Expert, 25)]
    public async Task Tolerance_DefaultsPerLevel(ExperienceLevel level, int cents)
    {
        var panel = RenderAt(level);

        await panel.Find("[data-testid=singing-toggle]").ClickAsync(new());

        SentRequest().ToleranceCents.ShouldBe(cents);
        panel.FindAll("[data-testid=singing-tolerance]").Count.ShouldBe(level == ExperienceLevel.Beginner ? 0 : 1);
        panel.FindAll("[data-testid=singing-raw]").Count.ShouldBe(level == ExperienceLevel.Expert ? 1 : 0);
    }

    [Fact]
    public async Task Advanced_Tolerance_ChangesTheRunningFeedback_AndIsRemembered()
    {
        var panel = RenderAt(ExperienceLevel.Advanced);
        await panel.Find("[data-testid=singing-toggle]").ClickAsync(new());

        var slider = panel.Find("[data-testid=singing-tolerance]");
        (slider.GetAttribute("min"), slider.GetAttribute("max"), slider.GetAttribute("step")).ShouldBe(("10", "200", "5"));
        await slider.ChangeAsync(new ChangeEventArgs { Value = "75" });

        await Views.Received(1).SetSingingToleranceAsync(75);
        panel.Find("[data-testid=singing-tolerance]").GetAttribute("aria-valuenow").ShouldBe("75");
        JSInterop.VerifyInvoke("localStorage.setItem").Arguments.ShouldBe(["stemstudio.singing.toleranceCents", "75"]);
    }

    [Fact]
    public void StoredTolerance_IsClamped()
    {
        JSInterop.Setup<string?>("localStorage.getItem", "stemstudio.singing.toleranceCents").SetResult("900");

        RenderAt(ExperienceLevel.Advanced).Find("[data-testid=singing-tolerance]").GetAttribute("aria-valuenow").ShouldBe("200");
    }

    [Theory]
    [InlineData(LatencyMethodDto.Loopback, "Latency: 42.0 ms (calibrated)")]
    [InlineData(LatencyMethodDto.Manual, "Latency: 42.0 ms (calibrated)")]
    [InlineData(LatencyMethodDto.Estimated, "Latency: 42.0 ms (estimated - calibrate in Recording settings)")]
    public void Advanced_Latency_SaysWhereItComesFrom(LatencyMethodDto method, string text)
    {
        _latency = new LatencyInUse(1852, method, null);

        var latency = RenderAt(ExperienceLevel.Advanced).Find("[data-testid=singing-latency]");

        latency.TextContent.ShouldContain(text);
        latency.GetAttribute("data-latency-source").ShouldBe(method.ToString().ToLowerInvariant());
    }

    [Fact]
    public void WithoutAnOpenInput_TheLatencyAsksForOne()
    {
        _latency = null;

        RenderAt(ExperienceLevel.Advanced).Find("[data-testid=singing-latency]").TextContent.ShouldContain("no microphone is open");
    }

    [Fact]
    public void Expert_TheRawReadout_FollowsThePitchView()
    {
        var raw = RenderAt(ExperienceLevel.Expert).Find("[data-testid=singing-raw]");

        raw.GetAttribute("data-singing-raw-for").ShouldBe("pitch-view-host");
        raw.GetAttribute("aria-live").ShouldBe("off");
    }

    [Fact]
    public async Task Dispose_StopsARunningFeedback()
    {
        var panel = RenderPanel();
        await panel.Find("[data-testid=singing-toggle]").ClickAsync(new());

        await panel.Instance.DisposeAsync();

        await Views.Received(1).StopSingingFeedbackAsync();
        _running.ShouldBe([true, false]);
    }

    [Fact]
    public void AnalysisError_IsShown()
    {
        Pitch(InstrumentViewsData.VocalPitch(AnalysisStatusDto.Failed) with { Reason = "The vocal stem is silent, so no melody can be shown." });

        var panel = RenderPanel();

        panel.Find("[data-testid=singing-reference-status]").TextContent.ShouldContain("silent");
        panel.Find("[data-testid=singing-reference-analyse]").TextContent.ShouldContain("Try again");
    }

    [Fact]
    public void Expert_EveryControlIsNamedWithItsVisibleText()
    {
        InstrumentViewsAccessibility.EveryControlIsNamedWithItsVisibleText(RenderAt(ExperienceLevel.Expert).Find("[data-testid=singing]"));
    }
}
