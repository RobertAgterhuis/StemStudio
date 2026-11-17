using Bunit;
using NSubstitute;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Tests.Fakes;
using StemStudio.Web.Tests.Pages;

namespace StemStudio.Web.Tests.Care;

/// <summary>The track page after the clean-up removed the detail stems (013 T060, FR-025; ui-test-ids 1.12.0 detail-files-removed).</summary>
public sealed class DetailFilesRemovedTests : TrackDetailTestContext
{
    private static readonly DateTimeOffset Removed = new(2026, 11, 17, 11, 0, 0, TimeSpan.Zero);

    private static TrackDto RemovedTrack(DetailPhaseStatusDto status = DetailPhaseStatusDto.Completed) =>
        TestData.Track() with { Detail = TestData.CompletedDetail() with { Status = status, FilesRemovedAt = Removed } };

    [Fact]
    public void RemovedDetail_ShowsTheNotice_InTheDetailSection_WithSplitFurther()
    {
        var page = RenderPage(RemovedTrack());

        var section = page.Find("[data-testid=detail-section]");
        var notice = section.QuerySelector("[data-testid=detail-files-removed]")!;
        notice.TextContent.ShouldBe("Detail stems removed - Split further to restore them.");
        notice.GetAttribute("data-help-id").ShouldBe("detail-files-removed");
        section.QuerySelector("[data-testid=detail-split-further]").ShouldNotBeNull();
        section.QuerySelectorAll("[data-testid^=detail-outcome-]").ShouldBeEmpty();
    }

    [Fact]
    public async Task SplitFurther_StartsTheDetailPhaseAgain()
    {
        Api.StartDetailAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(RemovedTrack(DetailPhaseStatusDto.Queued));
        var page = RenderPage(RemovedTrack());

        await page.Find("[data-testid=detail-split-further]").ClickAsync(new());

        await Api.Received(1).StartDetailAsync(TestData.TrackId, Arg.Any<CancellationToken>());
        page.FindAll("[data-testid=detail-split-further]").ShouldBeEmpty();
        page.Find("[data-testid=detail-files-removed]").ShouldNotBeNull();
    }

    [Fact]
    public void KeptDetail_HasNoNotice()
    {
        var page = RenderPage(TestData.DetailTrack());

        page.FindAll("[data-testid=detail-files-removed]").ShouldBeEmpty();
        page.Find("[data-testid=detail-outcome-drums]").ShouldNotBeNull();
    }
}
