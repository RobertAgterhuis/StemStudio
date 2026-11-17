using System.Net;
using Bunit;
using Microsoft.AspNetCore.Components;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Contracts.Mixes;
using StemStudio.Contracts.Practice;
using StemStudio.Web.Api;
using StemStudio.Web.Audio;
using StemStudio.Web.Experience;
using StemStudio.Web.Preferences;
using StemStudio.Web.Tests.Fakes;
using StemStudio.Web.Tests.Mixer;
using MixerPage = StemStudio.Web.Components.Mixer.Mixer;

namespace StemStudio.Web.Tests.Components.Practice;

/// <summary>T098: the practice log panel and the recorder wired to the mixer (FR-025, FR-027, FR-028; ui-test-ids 1.13.0 practice-log-*).</summary>
public sealed class PracticePanelTests : MixerTestContext
{
    public PracticePanelTests()
    {
        Api.GetPracticeLogSettingsAsync(Arg.Any<CancellationToken>()).Returns(PracticeFlowData.LogSettings());
        Api.GetPracticeSummaryAsync(TestData.TrackId, 30, Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(PracticeFlowData.Summary());
        Api.GetSectionsAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(PracticeFlowData.SectionSet());
    }

    private IRenderedComponent<CascadingValue<ExperienceContext>> RenderReady(ExperienceLevel level = ExperienceLevel.Advanced)
    {
        var page = Render<CascadingValue<ExperienceContext>>(p => p
            .Add(c => c.Value, new ExperienceContext(level, DensityPreference.Comfortable))
            .AddChildContent<MixerPage>(m => m.Add(x => x.Id, TestData.TrackId)));
        page.WaitForAssertion(() => page.Find("[data-testid=transport-play]").HasAttribute("disabled").ShouldBeFalse());
        page.WaitForAssertion(() => page.FindAll("[data-testid^=section-0b1c]").Count.ShouldBe(5));
        return page;
    }

    private static IRenderedComponent<CascadingValue<ExperienceContext>> Open(IRenderedComponent<CascadingValue<ExperienceContext>> page)
    {
        page.Find("[data-testid=practice-log-panel-toggle]").Click();
        page.WaitForAssertion(() => page.Find("[data-testid=practice-log-panel]").ShouldNotBeNull());
        return page;
    }

    private void Playback(PlaybackState state) => Engine.PlaybackStateChanged += Raise.Event<EventHandler<PlaybackState>>(Engine, state);

    private void Position(double seconds) => Engine.PositionChanged += Raise.Event<EventHandler<double>>(Engine, seconds);

    [Fact]
    public void Panel_ShowsTheTotalsTheSectionTableAndTheCharts()
    {
        var page = Open(RenderReady());

        page.Find("[data-testid=practice-log-panel]").GetAttribute("aria-label").ShouldBe("Practice log");
        (page.Find("[data-testid=practice-log-today]").TextContent, page.Find("[data-testid=practice-log-week]").TextContent, page.Find("[data-testid=practice-log-total]").TextContent)
            .ShouldBe(("12 min", "40 min", "2 h 30 min"));
        page.Find("[data-testid=practice-log-sections-table]").TextContent.ShouldContain("Guitar solo");
        page.Find("[data-testid=practice-log-sections-table]").TextContent.ShouldContain("95 %");
        page.Find("[data-testid=practice-log-chart-days]").ShouldNotBeNull();
        page.Find("[data-testid=practice-log-chart-tempo]").ShouldNotBeNull();
        page.Find("[data-testid=practice-log-panel]").TextContent.ShouldContain("Practised on 2 of the last 2 days");
        page.Find("[data-testid=practice-log-panel]").TextContent.ShouldContain("Guitar solo: 85 % to 95 %.");
        page.Find("[data-testid=practice-log-panel-toggle]").GetAttribute("aria-expanded").ShouldBe("true");
    }

    [Fact]
    public async Task ShowAsTable_SwapsBothChartsForTables()
    {
        var page = Open(RenderReady());

        await page.InvokeAsync(() => page.Find("[data-testid=practice-log-chart-as-table]").Click());

        page.Find("[data-testid=practice-log-chart-days-table]").TextContent.ShouldContain("17 Nov");
        page.Find("[data-testid=practice-log-chart-tempo-table]").TextContent.ShouldContain("95 %");
        page.FindAll("[data-testid=practice-log-chart-days]").ShouldBeEmpty();
        page.Find("[data-testid=practice-log-chart-as-table]").GetAttribute("aria-pressed").ShouldBe("true");
    }

    [Fact]
    public void Beginner_HasNoSectionTableOrTempoChart()
    {
        var page = Open(RenderReady(ExperienceLevel.Beginner));

        page.FindAll("[data-testid=practice-log-sections-table]").ShouldBeEmpty();
        page.FindAll("[data-testid=practice-log-chart-tempo]").ShouldBeEmpty();
        page.Find("[data-testid=practice-log-chart-days]").ShouldNotBeNull();
    }

    [Fact]
    public async Task OffNote_TurnOn_StartsTheRecorder()
    {
        Api.GetPracticeLogSettingsAsync(Arg.Any<CancellationToken>()).Returns(PracticeFlowData.LogSettings(enabled: false), PracticeFlowData.LogSettings(enabled: false),
            PracticeFlowData.LogSettings());
        Api.UpdatePracticeLogSettingsAsync(Arg.Any<PracticeLogSettingsUpdateRequest>(), Arg.Any<CancellationToken>()).Returns(PracticeFlowData.LogSettings());
        var page = Open(RenderReady());
        page.Find("[data-testid=practice-log-off-note]").ShouldNotBeNull();

        await page.InvokeAsync(() => page.Find("[data-testid=practice-log-turn-on]").Click());

        await Api.Received(1).UpdatePracticeLogSettingsAsync(Arg.Is<PracticeLogSettingsUpdateRequest>(r => r.Enabled == true), Arg.Any<CancellationToken>());
        page.FindAll("[data-testid=practice-log-off-note]").ShouldBeEmpty();
        page.WaitForAssertion(() => Api.Received(3).GetPracticeLogSettingsAsync(Arg.Any<CancellationToken>()));
    }

    [Fact]
    public async Task FirstUseNotice_OkAndTurnOff()
    {
        Api.GetPracticeLogSettingsAsync(Arg.Any<CancellationToken>()).Returns(PracticeFlowData.LogSettings(acknowledged: false));
        Api.UpdatePracticeLogSettingsAsync(Arg.Any<PracticeLogSettingsUpdateRequest>(), Arg.Any<CancellationToken>())
            .Returns(PracticeFlowData.LogSettings(), PracticeFlowData.LogSettings(enabled: false));
        var page = Open(RenderReady());
        page.Find("[data-testid=practice-log-notice]").TextContent.ShouldContain("only on this computer");

        await page.InvokeAsync(() => page.Find("[data-testid=practice-log-notice-ok]").Click());
        page.FindAll("[data-testid=practice-log-notice]").ShouldBeEmpty();
        await Api.Received(1).UpdatePracticeLogSettingsAsync(Arg.Is<PracticeLogSettingsUpdateRequest>(r => r.NoticeAcknowledged == true && r.Enabled == null), Arg.Any<CancellationToken>());

        await page.InvokeAsync(() => page.Find("[data-testid=practice-log-panel-toggle]").Click());
        Api.GetPracticeLogSettingsAsync(Arg.Any<CancellationToken>()).Returns(PracticeFlowData.LogSettings(acknowledged: false));
        Open(page);
        await page.InvokeAsync(() => page.Find("[data-testid=practice-log-notice-turn-off]").Click());
        page.Find("[data-testid=practice-log-off-note]").ShouldNotBeNull();
    }

    [Fact]
    public async Task DeleteThisSongsLog_AfterConfirmation()
    {
        var page = Open(RenderReady());

        await page.InvokeAsync(() => page.Find("[data-testid=practice-log-delete-track]").Click());
        await page.InvokeAsync(() => page.Find("[data-testid=practice-log-delete-confirm]").Click());

        await Api.Received(1).DeletePracticeSessionsAsync(TestData.TrackId, Arg.Any<CancellationToken>());
        page.FindAll("[data-testid=practice-log-delete-confirm]").ShouldBeEmpty();
    }

    [Fact]
    public void AnApiError_IsShownInThePanel()
    {
        Api.GetPracticeSummaryAsync(TestData.TrackId, 30, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ApiException(new ApiError(HttpStatusCode.ServiceUnavailable, "Down", "The API is down.", new Dictionary<string, string[]>())));

        var page = Open(RenderReady());

        page.Find("[data-testid=practice-log-error]").TextContent.ShouldBe("The API is down.");
    }

    [Fact]
    public async Task Recorder_RecordsPlayingTimeSectionsAndLoopPasses_AndFlushesOnClose()
    {
        var page = RenderReady();
        Position(42);
        page.WaitForAssertion(() => page.Find($"[data-testid=section-{PracticeFlowData.SectionIds[3]}]").GetAttribute("data-current").ShouldBe("true"));
        await page.InvokeAsync(() => page.Find($"[data-testid=section-{PracticeFlowData.SectionIds[3]}]").Click());

        Playback(PlaybackState.Playing);
        Time.Advance(TimeSpan.FromSeconds(5));
        Position(47);
        Engine.LoopPassed += Raise.Event<EventHandler<LoopPassInfo>>(Engine, new LoopPassInfo(40.1, 56.1, 1, 100));
        Time.Advance(TimeSpan.FromSeconds(3));
        Playback(PlaybackState.Paused);

        page.WaitForAssertion(() => Api.Received(1).UpsertPracticeSessionAsync(Arg.Any<Guid>(), Arg.Is<PracticeSessionInputDto>(s =>
            s.TrackId == TestData.TrackId && Math.Abs(s.PlayedSeconds - 8) < 0.01 && s.LoopPasses == 1
            && s.Sections!.Single().SectionId == PracticeFlowData.SectionIds[3]), Arg.Any<CancellationToken>()));

        Playback(PlaybackState.Playing);
        Time.Advance(TimeSpan.FromSeconds(2));
        await DisposeComponentsAsync();
        await Api.Received(2).UpsertPracticeSessionAsync(Arg.Any<Guid>(), Arg.Any<PracticeSessionInputDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Recorder_DoesNothing_WhileTheLogIsOff()
    {
        Api.GetPracticeLogSettingsAsync(Arg.Any<CancellationToken>()).Returns(PracticeFlowData.LogSettings(enabled: false));
        RenderReady();

        Playback(PlaybackState.Playing);
        Time.Advance(TimeSpan.FromSeconds(40));
        Position(40);
        Playback(PlaybackState.Paused);

        Api.DidNotReceive().UpsertPracticeSessionAsync(Arg.Any<Guid>(), Arg.Any<PracticeSessionInputDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Recorder_LoopPassWithoutTheTrainer_CountsButRecordsNoSpeed()
    {
        Api.GetMixAsync(TestData.MixId, Arg.Any<CancellationToken>()).Returns(TestData.Mix());
        var page = RenderReady();
        await page.InvokeAsync(() => page.Find($"[data-testid=section-{PracticeFlowData.SectionIds[3]}]").Click());

        Playback(PlaybackState.Playing);
        Time.Advance(TimeSpan.FromSeconds(1));
        Position(41);
        Engine.LoopPassed += Raise.Event<EventHandler<LoopPassInfo>>(Engine, new LoopPassInfo(40.1, 56.1, 1, 90));
        Playback(PlaybackState.Stopped);

        page.WaitForAssertion(() => Api.Received(1).UpsertPracticeSessionAsync(Arg.Any<Guid>(), Arg.Is<PracticeSessionInputDto>(s =>
            s.Sections!.Single(r => r.SectionId == PracticeFlowData.SectionIds[3]).LoopPasses == 1
            && s.Sections!.Single(r => r.SectionId == PracticeFlowData.SectionIds[3]).TrainerReachedPercent == null), Arg.Any<CancellationToken>()));
    }

    [Fact]
    public void Recorder_WithoutSettings_StaysOff()
    {
        Api.GetPracticeLogSettingsAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new ApiException("down"));
        RenderReady();

        Playback(PlaybackState.Playing);
        Time.Advance(TimeSpan.FromSeconds(5));
        Playback(PlaybackState.Paused);

        Api.DidNotReceive().UpsertPracticeSessionAsync(Arg.Any<Guid>(), Arg.Any<PracticeSessionInputDto>(), Arg.Any<CancellationToken>());
        _ = new LoopRegionDto(0, 1, true);
    }
}
