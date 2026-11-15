using Bunit;
using NSubstitute;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Components.Pages;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Pages;

/// <summary>Detail phase section of the track detail page (FR-007, FR-017, US1, US3 AS1).</summary>
public sealed class TrackDetailDetailTests : PageTestContext
{
    private const string Section = "[data-testid=detail-section]";
    private const string Status = "[data-testid=detail-status]";
    private const string Stage = "[data-testid=detail-stage]";
    private const string Progress = "[data-testid=detail-progress]";
    private const string LongNotice = "[data-testid=detail-long-notice]";

    private IRenderedComponent<TrackDetail> RenderPage(TrackDto first, params TrackDto[] next)
    {
        Api.GetTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(first, next);
        return Render<TrackDetail>(p => p.Add(c => c.Id, TestData.TrackId));
    }

    [Theory]
    [InlineData(DetailPhaseStatusDto.None, "none")]
    [InlineData(DetailPhaseStatusDto.Queued, "queued")]
    [InlineData(DetailPhaseStatusDto.Running, "running")]
    [InlineData(DetailPhaseStatusDto.Completed, "completed")]
    [InlineData(DetailPhaseStatusDto.Failed, "failed")]
    [InlineData(DetailPhaseStatusDto.Cancelled, "cancelled")]
    public void Status_IsShownAsContractText(DetailPhaseStatusDto status, string text)
    {
        var page = RenderPage(TestData.WithDetail(TestData.Phase(status)));

        page.Find(Status).TextContent.ShouldBe(text);
    }

    [Fact]
    public void Section_IsNotShown_WhileTheStandardSeparationRuns()
    {
        var page = RenderPage(TestData.Track(TrackStatusDto.Separating));

        page.FindAll(Section).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(DetailPhaseStatusDto.Queued, 0, "Waiting for standard separations")]
    [InlineData(DetailPhaseStatusDto.Running, 42, "Splitting drums (2 of 4)")]
    public void Active_ShowsStageProgressAndLongNotice(DetailPhaseStatusDto status, int progress, string stage)
    {
        var page = RenderPage(TestData.WithDetail(TestData.Phase(status, progress, stage)));

        page.Find(Stage).TextContent.ShouldBe(stage);
        page.Find(Progress).GetAttribute("aria-valuenow").ShouldBe(progress.ToString(System.Globalization.CultureInfo.InvariantCulture));
        page.Find(LongNotice).TextContent.ShouldContain("considerably longer than the standard separation");
        page.Find(LongNotice).TextContent.ShouldContain("standard stems stay playable");
    }

    [Fact]
    public void Completed_HidesProgressAndNotice_AndShowsEveryOutcomeText()
    {
        var page = RenderPage(TestData.DetailTrack());

        page.FindAll(Progress).ShouldBeEmpty();
        page.FindAll(LongNotice).ShouldBeEmpty();
        AssertOutcome(page, "drums", "split", "Split into kick, snare");
        AssertOutcome(page, "vocals", "split", "Split into lead, backing");
        AssertOutcome(page, "bass", "not-supported", "Not split");
        AssertOutcome(page, "guitar", "nothing-further", "Nothing further to separate");
        AssertOutcome(page, "piano", "skipped-silent", "Skipped: (almost) silent");
        AssertOutcome(page, "other", "not-reliable", "Split was not reliable");
    }

    [Fact]
    public void Completed_ListsDetailStemsPerParent_InDisplayOrder()
    {
        var page = RenderPage(TestData.DetailTrack());

        page.FindAll("[data-testid^=detail-stem-]").Select(e => e.GetAttribute("data-testid")).ShouldBe(
        [
            "detail-stem-drums-kick", "detail-stem-drums-snare", "detail-stem-drums-rest",
            "detail-stem-vocals-lead", "detail-stem-vocals-backing", "detail-stem-vocals-rest",
        ]);
        page.Find("[data-testid=detail-stem-drums-snare]").TextContent.ShouldBe("snare");
    }

    [Fact]
    public void LongSong_ShowsNotice_BeforeTheDetailPhaseRuns()
    {
        var page = RenderPage(TestData.Track() with { DurationSeconds = 660 });

        page.Find(LongNotice).TextContent.ShouldContain("longer than 10 minutes");
        page.FindAll(Progress).ShouldBeEmpty();
    }

    [Fact]
    public void LongSong_WhileRunning_ShowsBothNotices()
    {
        var track = TestData.WithDetail(TestData.Phase(DetailPhaseStatusDto.Running, 5)) with { DurationSeconds = 840 };

        var notice = RenderPage(track).Find(LongNotice).TextContent;

        notice.ShouldContain("considerably longer");
        notice.ShouldContain("longer than 10 minutes");
    }

    [Fact]
    public void ShortSong_WithoutActivePhase_ShowsNoNotice()
    {
        var page = RenderPage(TestData.Track());

        page.FindAll(LongNotice).ShouldBeEmpty();
    }

    [Fact]
    public void LimitationHelp_IsShown(/* FR-017 */)
    {
        var help = RenderPage(TestData.Track()).Find("[data-testid=detail-limitation-help]").TextContent;

        help.ShouldContain("quality varies per song and instrument");
        help.ShouldContain("cannot be separated");
        help.ShouldContain("considerably longer than the standard separation");
    }

    [Fact]
    public void Polling_ContinuesWhileTheDetailPhaseIsActive_OnACompletedTrack()
    {
        var page = RenderPage(
            TestData.WithDetail(TestData.Phase(DetailPhaseStatusDto.Queued, 0, "Queued")),
            TestData.WithDetail(TestData.Phase(DetailPhaseStatusDto.Running, 10, "Splitting drums (1 of 3)")),
            TestData.DetailTrack());

        Timer.Tick();
        page.WaitForAssertion(() => page.Find(Status).TextContent.ShouldBe("running"));
        Timer.Tick();
        page.WaitForAssertion(() => page.Find(Status).TextContent.ShouldBe("completed"));

        Timer.Waits.ShouldBe(2);
        page.Find("[data-testid=detail-outcome-drums]").GetAttribute("data-outcome").ShouldBe("split");
    }

    [Fact]
    public void Polling_ContinuesFromStandardIntoTheAutoQueuedDetailPhase()
    {
        var page = RenderPage(
            TestData.Track(TrackStatusDto.Analysing),
            TestData.WithDetail(TestData.Phase(DetailPhaseStatusDto.Queued, 0, "Queued")),
            TestData.DetailTrack());

        Timer.Tick();
        page.WaitForAssertion(() => page.Find(Status).TextContent.ShouldBe("queued"));
        Timer.Tick();
        page.WaitForAssertion(() => page.Find(Status).TextContent.ShouldBe("completed"));
        Timer.Waits.ShouldBe(2);
    }

    private static void AssertOutcome(IRenderedComponent<TrackDetail> page, string parent, string kind, string text)
    {
        var outcome = page.Find($"[data-testid=detail-outcome-{parent}]");
        outcome.GetAttribute("data-outcome").ShouldBe(kind);
        outcome.TextContent.ShouldBe(text);
    }
}
