using Bunit;
using NSubstitute;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Pages;

/// <summary>"Split further" for completed tracks without a detail phase (FR-009, US4).</summary>
public sealed class TrackDetailSplitFurtherTests : TrackDetailTestContext
{
    private const string SplitFurther = "[data-testid=detail-split-further]";

    [Theory]
    [InlineData(DetailPhaseStatusDto.None, true)]
    [InlineData(DetailPhaseStatusDto.Queued, false)]
    [InlineData(DetailPhaseStatusDto.Running, false)]
    [InlineData(DetailPhaseStatusDto.Completed, false)]
    [InlineData(DetailPhaseStatusDto.Failed, false)]
    [InlineData(DetailPhaseStatusDto.Cancelled, false)]
    public void SplitFurther_IsOfferedOnlyForStatusNone(DetailPhaseStatusDto status, bool shown)
    {
        var page = RenderPage(TestData.WithDetail(TestData.Phase(status)));

        (page.FindAll(SplitFurther).Count == 1).ShouldBe(shown);
    }

    [Fact]
    public async Task SplitFurther_QueuesTheDetailPhase_AndShowsProgress()
    {
        Api.StartDetailAsync(TestData.TrackId, Arg.Any<CancellationToken>())
            .Returns(TestData.WithDetail(TestData.Phase(DetailPhaseStatusDto.Queued, 0, "Queued")));
        var page = RenderPage(TestData.Track(), TestData.WithDetail(TestData.Phase(DetailPhaseStatusDto.Running, 25, "Splitting drums (1 of 3)")));
        Timer.Waits.ShouldBe(0);

        await page.Find(SplitFurther).ClickAsync(new());

        await Api.Received(1).StartDetailAsync(TestData.TrackId, Arg.Any<CancellationToken>());
        page.Find("[data-testid=detail-status]").TextContent.ShouldBe("queued");
        page.FindAll(SplitFurther).ShouldBeEmpty();
        Timer.Tick();
        page.WaitForAssertion(() => page.Find("[data-testid=detail-progress]").GetAttribute("aria-valuenow").ShouldBe("25"));
        page.Find("[data-testid=detail-stage]").TextContent.ShouldBe("Splitting drums (1 of 3)");
    }
}
