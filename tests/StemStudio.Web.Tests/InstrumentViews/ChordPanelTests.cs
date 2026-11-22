using System.Net;
using Bunit;
using NSubstitute;
using StemStudio.Contracts.InstrumentViews;
using StemStudio.Web.Api;
using StemStudio.Web.Audio;
using StemStudio.Web.Components.InstrumentViews;
using StemStudio.Web.Experience;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.InstrumentViews;

/// <summary>Chords in the mixer (T070, FR-012..FR-014, FR-064, ui-test-ids 1.15.0).</summary>
public sealed class ChordPanelTests : PageTestContext
{
    private static readonly Guid Track = TestData.TrackId;

    private void Chords(params TrackAnalysisDto<ChordChartDocumentDto>[] answers) =>
        Api.GetChordsAsync(Track, Arg.Any<CancellationToken>()).Returns(answers[0], answers[1..]);

    private IRenderedComponent<ChordPanel> RenderPanel(bool engineReady = true, Action<TrackAnalysisDto<ChordChartDocumentDto>>? loaded = null) =>
        Render<ChordPanel>(p => p.Add(c => c.TrackId, Track).Add(c => c.EngineReady, engineReady)
            .Add(c => c.OnLoaded, a => loaded?.Invoke(a)));

    private IRenderedComponent<Microsoft.AspNetCore.Components.CascadingValue<ExperienceContext>> RenderAt(ExperienceLevel level) =>
        RenderAtLevel<ChordPanel>(level, p => p.Add(c => c.TrackId, Track).Add(c => c.EngineReady, true));

    [Fact]
    public void Ready_ShowsTheEmptyLaneHost_AttachesIt_AndTheLimits()
    {
        Chords(InstrumentViewsData.Chords());
        TrackAnalysisDto<ChordChartDocumentDto>? loaded = null;

        var panel = RenderPanel(loaded: a => loaded = a);

        var lane = panel.Find("[data-testid=chord-lane]");
        lane.Id.ShouldBe("chord-lane-host");
        lane.ChildElementCount.ShouldBe(0);
        lane.GetAttribute("data-help-id").ShouldBe("chords");
        panel.Find("[data-testid=chords-status]").GetAttribute("data-status").ShouldBe("ready");
        panel.Find("[data-testid=chords-limits]").TextContent.ShouldContain("80 %");
        panel.Find("[data-testid=chord-lane-toggle]").GetAttribute("aria-pressed").ShouldBe("true");
        Views.Received(1).AttachChordLaneAsync("chord-lane-host", Arg.Is<ChordChartData>(c => c.Segments.Count == 2 && c.KeyTonic == 0), Arg.Any<ChordLaneOptions>());
        loaded!.Status.ShouldBe(AnalysisStatusDto.Ready);
    }

    [Fact]
    public void Expert_EveryControlIsNamedWithItsVisibleText()
    {
        Chords(InstrumentViewsData.Chords());

        InstrumentViewsAccessibility.EveryControlIsNamedWithItsVisibleText(RenderAt(ExperienceLevel.Expert).Find(".ss-chords"));
    }

    [Fact]
    public void BeforeTheEngineIsReady_TheLaneIsNotAttached()
    {
        Chords(InstrumentViewsData.Chords());

        RenderPanel(engineReady: false);

        Views.DidNotReceiveWithAnyArgs().AttachChordLaneAsync(default!, default!, default!);
    }

    [Fact]
    public async Task Toggle_HidesTheLane_DetachesIt_AndRemembersTheChoice()
    {
        Chords(InstrumentViewsData.Chords());
        var panel = RenderPanel();

        await panel.Find("[data-testid=chord-lane-toggle]").ClickAsync(new());

        panel.FindAll("[data-testid=chord-lane]").ShouldBeEmpty();
        panel.Find("[data-testid=chord-lane-toggle]").GetAttribute("aria-pressed").ShouldBe("false");
        await Views.Received(1).DetachChordLaneAsync();
        JSInterop.VerifyInvoke("localStorage.setItem").Arguments.ShouldBe(["stemstudio.chords.lane", "false"]);
    }

    [Fact]
    public void StoredChoice_HidesTheLane()
    {
        JSInterop.Setup<string?>("localStorage.getItem", "stemstudio.chords.lane").SetResult("false");
        Chords(InstrumentViewsData.Chords());

        RenderPanel().FindAll("[data-testid=chord-lane]").ShouldBeEmpty();
    }

    [Fact]
    public void Running_ShowsProgress_PollsUntilReady()
    {
        Chords(InstrumentViewsData.Chords(AnalysisStatusDto.Running, 0.4), InstrumentViewsData.Chords());

        var panel = RenderPanel();
        panel.Find("[data-testid=chords-status]").TextContent.ShouldBe("Detecting chords: 40 %.");
        panel.FindAll("[data-testid=chord-lane]").ShouldBeEmpty();
        panel.FindAll("[data-testid=chords-detect]").ShouldBeEmpty();

        Timer.Tick();

        panel.WaitForAssertion(() => panel.Find("[data-testid=chords-status]").GetAttribute("data-status").ShouldBe("ready"));
        panel.Find("[data-testid=chord-lane]");
    }

    [Theory]
    [InlineData(AnalysisStatusDto.None, "Detect chords")]
    [InlineData(AnalysisStatusDto.Failed, "Try again")]
    [InlineData(AnalysisStatusDto.Stale, "Detect chords")]
    public async Task Detect_IsOfferedWithoutReadyChords_AndStartsDetection(AnalysisStatusDto status, string text)
    {
        Chords(InstrumentViewsData.Chords(status));
        Api.DetectChordsAsync(Track, Arg.Any<DetectChordsRequest>(), Arg.Any<CancellationToken>()).Returns(InstrumentViewsData.Chords(AnalysisStatusDto.Queued));
        var panel = RenderPanel();

        var detect = panel.Find("[data-testid=chords-detect]");
        detect.TextContent.ShouldContain(text);
        await detect.ClickAsync(new());

        await Api.Received(1).DetectChordsAsync(Track, Arg.Is<DetectChordsRequest>(r => !r.OverwriteEdits && r.Vocabulary == ChordVocabularyDto.Simple && r.Engine == null), Arg.Any<CancellationToken>());
        panel.Find("[data-testid=chords-status]").GetAttribute("data-status").ShouldBe("queued");
    }

    [Fact]
    public void Ready_AtBeginner_OffersNoRedetection_NorVocabularyOrEngine()
    {
        Chords(InstrumentViewsData.Chords());

        var page = RenderAt(ExperienceLevel.Beginner);

        page.FindAll("[data-testid=chords-detect]").ShouldBeEmpty();
        page.FindAll("[data-testid=chords-vocabulary]").ShouldBeEmpty();
        page.FindAll("[data-testid=chords-engine]").ShouldBeEmpty();
        page.Find("[data-testid=chord-lane]");
    }

    [Fact]
    public void Advanced_HasVocabulary_AndRedetect_ButNoEngine()
    {
        Chords(InstrumentViewsData.Chords());

        var page = RenderAt(ExperienceLevel.Advanced);

        page.Find("[data-testid=chords-detect]").TextContent.ShouldContain("Detect again");
        page.Find("[data-testid=chords-vocabulary]").GetAttribute("data-help-id").ShouldBe("chords-vocabulary");
        page.FindAll("[data-testid=chords-engine]").ShouldBeEmpty();
    }

    [Fact]
    public async Task Expert_ChoosesVocabularyAndEngine_ForTheNextDetection()
    {
        Chords(InstrumentViewsData.Chords());
        Api.DetectChordsAsync(Track, Arg.Any<DetectChordsRequest>(), Arg.Any<CancellationToken>()).Returns(InstrumentViewsData.Chords(AnalysisStatusDto.Queued));
        var page = RenderAt(ExperienceLevel.Expert);

        await page.Find("[data-testid=chords-vocabulary]").ChangeAsync(new() { Value = "Full" });
        await page.Find("[data-testid=chords-engine]").ChangeAsync(new() { Value = "ChromaHmm" });
        await page.Find("[data-testid=chords-detect]").ClickAsync(new());

        await Api.Received(1).DetectChordsAsync(Track, Arg.Is<DetectChordsRequest>(r => r.Vocabulary == ChordVocabularyDto.Full && r.Engine == ChordEngineDto.ChromaHmm), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Redetect_WithEdits_AsksFirst_AndOverwritesOnlyWhenConfirmed()
    {
        var edited = InstrumentViewsData.Chords() with { Document = InstrumentViewsData.Chords().Document! with { HasEdits = true } };
        Chords(edited);
        Api.DetectChordsAsync(Track, Arg.Any<DetectChordsRequest>(), Arg.Any<CancellationToken>()).Returns(InstrumentViewsData.Chords(AnalysisStatusDto.Queued));
        Confirm.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), "Replace", "chords-overwrite-confirm").Returns(false, true);
        var page = RenderAt(ExperienceLevel.Advanced);

        await page.Find("[data-testid=chords-detect]").ClickAsync(new());
        Api.ReceivedCalls().ShouldNotContain(c => c.GetMethodInfo().Name == nameof(IStemStudioApiClient.DetectChordsAsync));

        await page.Find("[data-testid=chords-detect]").ClickAsync(new());
        await Api.Received(1).DetectChordsAsync(Track, Arg.Is<DetectChordsRequest>(r => r.OverwriteEdits), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StaleEditedChart_StaysOnTheLane_ShowsWhy_AndDetectingAsksBeforeReplacingTheEdits()
    {
        Chords(InstrumentViewsData.StaleEditedChords());
        var panel = RenderPanel();

        panel.Find("[data-testid=chord-lane]");
        await Views.Received(1).AttachChordLaneAsync("chord-lane-host", Arg.Is<ChordChartData>(c => c.Segments.Count == 2), Arg.Any<ChordLaneOptions>());
        var status = panel.Find("[data-testid=chords-status]");
        (status.GetAttribute("data-status"), status.TextContent).ShouldBe(("stale", "The stems changed since the chords were detected; detect them again. The drum stems were split again."));

        await panel.Find("[data-testid=chords-detect]").ClickAsync(new());

        await Confirm.Received(1).ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), "Replace", "chords-overwrite-confirm");
        Api.ReceivedCalls().ShouldNotContain(c => c.GetMethodInfo().Name == nameof(IStemStudioApiClient.DetectChordsAsync));
    }

    [Fact]
    public async Task EditsExistAnswer_AsksAndRetriesWithOverwrite()
    {
        Chords(InstrumentViewsData.Chords(AnalysisStatusDto.Stale));
        Api.DetectChordsAsync(Track, Arg.Is<DetectChordsRequest>(r => !r.OverwriteEdits), Arg.Any<CancellationToken>())
            .Returns<TrackAnalysisDto<ChordChartDocumentDto>>(_ => throw Problem409("edits-exist"));
        Api.DetectChordsAsync(Track, Arg.Is<DetectChordsRequest>(r => r.OverwriteEdits), Arg.Any<CancellationToken>()).Returns(InstrumentViewsData.Chords(AnalysisStatusDto.Queued));
        Confirm.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), "Replace", "chords-overwrite-confirm").Returns(true);
        var panel = RenderPanel();

        await panel.Find("[data-testid=chords-detect]").ClickAsync(new());

        await Api.Received(1).DetectChordsAsync(Track, Arg.Is<DetectChordsRequest>(r => r.OverwriteEdits), Arg.Any<CancellationToken>());
        panel.FindAll("[role=alert]").ShouldBeEmpty();
    }

    [Fact]
    public async Task OtherErrors_AreShown()
    {
        Chords(InstrumentViewsData.Chords(AnalysisStatusDto.None));
        Api.DetectChordsAsync(Track, Arg.Any<DetectChordsRequest>(), Arg.Any<CancellationToken>())
            .Returns<TrackAnalysisDto<ChordChartDocumentDto>>(_ => throw Problem409("track-not-ready", "The track is still being separated."));
        var panel = RenderPanel();

        await panel.Find("[data-testid=chords-detect]").ClickAsync(new());

        panel.Find("[role=alert]").TextContent.ShouldContain("still being separated");
    }

    [Fact]
    public void EngineError_OnAttach_IsShown()
    {
        Chords(InstrumentViewsData.Chords());
        Views.AttachChordLaneAsync(Arg.Any<string>(), Arg.Any<ChordChartData>(), Arg.Any<ChordLaneOptions>())
            .Returns(Task.FromException(new AudioEngineException("Invalid chord chart: unknown quality")));

        var panel = RenderPanel();

        panel.WaitForAssertion(() => panel.Find("[role=alert]").TextContent.ShouldContain("Invalid chord chart"));
    }

    [Fact]
    public void NoChords_ShowsNoLane()
    {
        Chords(InstrumentViewsData.Chords() with { Document = InstrumentViewsData.Chords().Document! with { NoChords = true } });

        RenderPanel().FindAll("[data-testid=chord-lane]").ShouldBeEmpty();
    }

    [Fact]
    public async Task Dispose_DetachesTheLane()
    {
        Chords(InstrumentViewsData.Chords());
        var panel = RenderPanel();

        await panel.Instance.DisposeAsync();

        await Views.Received(1).DetachChordLaneAsync();
    }

    private static ApiException Problem409(string code, string detail = "Conflict.") =>
        new(new ApiError(HttpStatusCode.Conflict, "Conflict", detail, new Dictionary<string, string[]>()) { Type = $"https://stemstudio.local/problems/{code}" });
}
