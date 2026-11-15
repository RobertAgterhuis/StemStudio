using System.Net;
using Bunit;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Components.Pages;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Pages;

/// <summary>Retry, cancel and failure reason of the detail phase; delete refused while it runs (FR-007, FR-008, FR-016).</summary>
public sealed class TrackDetailDetailActionsTests : PageTestContext
{
    private const string Retry = "[data-testid=detail-retry]";
    private const string Cancel = "[data-testid=detail-cancel]";
    private const string Status = "[data-testid=detail-status]";
    private const string DetailError = "[data-testid=detail-error]";
    private const string ActionError = "[data-testid=track-action-error]";

    private static readonly TrackDto Queued = Detail(DetailPhaseStatusDto.Queued);
    private static readonly TrackDto Running = Detail(DetailPhaseStatusDto.Running, progress: 30);

    private static TrackDto Detail(DetailPhaseStatusDto status, int progress = 0, string? error = null) =>
        TestData.WithDetail(TestData.Phase(status, progress, status.ToString(), error));

    private IRenderedComponent<TrackDetail> RenderPage(TrackDto first, params TrackDto[] next)
    {
        Api.GetTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(first, next);
        return Render<TrackDetail>(p => p.Add(c => c.Id, TestData.TrackId));
    }

    [Theory]
    [InlineData(DetailPhaseStatusDto.None, false, false)]
    [InlineData(DetailPhaseStatusDto.Queued, false, true)]
    [InlineData(DetailPhaseStatusDto.Running, false, true)]
    [InlineData(DetailPhaseStatusDto.Completed, false, false)]
    [InlineData(DetailPhaseStatusDto.Failed, true, false)]
    [InlineData(DetailPhaseStatusDto.Cancelled, true, false)]
    public void RetryAndCancel_AreOfferedPerStatus(DetailPhaseStatusDto status, bool retry, bool cancel)
    {
        var page = RenderPage(Detail(status));

        (page.FindAll(Retry).Count == 1).ShouldBe(retry);
        (page.FindAll(Cancel).Count == 1).ShouldBe(cancel);
    }

    [Fact]
    public void Failed_ShowsTheReason()
    {
        var page = RenderPage(Detail(DetailPhaseStatusDto.Failed, 40, "Detail model could not be downloaded; try again when online."));

        page.Find(DetailError).TextContent.ShouldBe("Detail model could not be downloaded; try again when online.");
        page.Find("[data-testid=track-status]").TextContent.ShouldBe("Completed");
        page.Find("[data-testid=open-mixer]").ShouldNotBeNull();
    }

    [Fact]
    public void NotFailed_ShowsNoDetailError()
    {
        RenderPage(Detail(DetailPhaseStatusDto.Cancelled)).FindAll(DetailError).ShouldBeEmpty();
    }

    [Fact]
    public async Task Retry_QueuesOnlyTheDetailPhase_AndPollsUntilCompleted()
    {
        Api.StartDetailAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(Queued);
        var page = RenderPage(Detail(DetailPhaseStatusDto.Failed, error: "interrupted by restart"), TestData.DetailTrack());

        await page.Find(Retry).ClickAsync(new());

        page.Find(Status).TextContent.ShouldBe("queued");
        page.FindAll(DetailError).ShouldBeEmpty();
        await Api.DidNotReceive().RetryTrackAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        Timer.Tick();
        page.WaitForAssertion(() => page.Find(Status).TextContent.ShouldBe("completed"));
    }

    [Fact]
    public async Task Retry_WhileRunning_IsBusy()
    {
        var pending = new TaskCompletionSource<TrackDto>();
        Api.StartDetailAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(pending.Task);
        var page = RenderPage(Detail(DetailPhaseStatusDto.Cancelled));

        var click = page.Find(Retry).ClickAsync(new());

        page.Find(Retry).HasAttribute("disabled").ShouldBeTrue();
        pending.SetResult(Queued);
        await click;
        page.Find(Status).TextContent.ShouldBe("queued");
    }

    [Fact]
    public async Task Cancel_CancelsThePhase_AndStopsPolling()
    {
        Api.CancelDetailAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(Detail(DetailPhaseStatusDto.Cancelled));
        var page = RenderPage(Running, Detail(DetailPhaseStatusDto.Cancelled));

        await page.Find(Cancel).ClickAsync(new());

        page.Find(Status).TextContent.ShouldBe("cancelled");
        page.Find(Retry).ShouldNotBeNull();
        Timer.Tick();
        page.WaitForAssertion(() => Timer.Waits.ShouldBe(1));
        await Api.Received(1).CancelDetailAsync(TestData.TrackId, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(HttpStatusCode.Conflict, "The detail phase is not queued or running.")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "The processing service is unavailable. Existing tracks and mixes remain usable.")]
    public async Task CancelRejected_ShowsTheReason(HttpStatusCode status, string reason)
    {
        Api.CancelDetailAsync(TestData.TrackId, Arg.Any<CancellationToken>()).ThrowsAsync(Problem(status, "Problem", reason));
        var page = RenderPage(Queued);

        await page.Find(Cancel).ClickAsync(new());

        page.Find(ActionError).TextContent.ShouldBe(reason);
        page.Find(Status).TextContent.ShouldBe("queued");
    }

    [Fact]
    public async Task RetryRejected_ShowsTheReason()
    {
        Api.StartDetailAsync(TestData.TrackId, Arg.Any<CancellationToken>())
            .ThrowsAsync(Problem(HttpStatusCode.Conflict, "Conflict", "The detail phase is already completed."));
        var page = RenderPage(Detail(DetailPhaseStatusDto.Failed, error: "Unexpected error"));

        await page.Find(Retry).ClickAsync(new());

        page.Find(ActionError).TextContent.ShouldBe("The detail phase is already completed.");
    }

    [Fact]
    public async Task DeleteRefused_WhileTheDetailPhaseIsActive_ShowsBlockedReason()
    {
        Api.DeleteTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>())
            .ThrowsAsync(Problem(HttpStatusCode.Conflict, "Detail phase active", "The detail phase of this track is still running."));
        var page = RenderPage(Running);

        await page.Find("[data-testid=track-delete]").ClickAsync(new());
        await page.Find("[data-testid=track-delete-confirm]").ClickAsync(new());

        page.Find("[data-testid=track-delete-blocked]").TextContent.ShouldContain("The detail phase of this track is still running.");
        page.Find("[data-testid=track-delete-blocked]").TextContent.ShouldContain("Cancel the detail phase");
        page.FindAll(ActionError).ShouldBeEmpty();
    }

    [Fact]
    public async Task DeleteBlocked_DisappearsAfterCancel()
    {
        Api.DeleteTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>())
            .ThrowsAsync(Problem(HttpStatusCode.Conflict, "Detail phase active", "Still running."));
        Api.CancelDetailAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(Detail(DetailPhaseStatusDto.Cancelled));
        var page = RenderPage(Queued);
        await page.Find("[data-testid=track-delete]").ClickAsync(new());
        await page.Find("[data-testid=track-delete-confirm]").ClickAsync(new());

        await page.Find(Cancel).ClickAsync(new());

        page.FindAll("[data-testid=track-delete-blocked]").ShouldBeEmpty();
    }
}
