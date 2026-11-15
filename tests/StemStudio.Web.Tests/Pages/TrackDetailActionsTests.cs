using System.Net;
using Bunit;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Components.Pages;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Pages;

/// <summary>Retry feedback and delete on the track detail page.</summary>
public sealed class TrackDetailActionsTests : PageTestContext
{
    private const string RetryButton = "[data-testid=track-retry]";
    private const string FailedAgain = "[data-testid=track-retry-failed-again]";
    private const string DeleteButton = "[data-testid=track-delete]";
    private const string DeleteConfirm = "[data-testid=track-delete-confirm]";
    private const string DeleteCancel = "[data-testid=track-delete-cancel]";
    private const string ActionError = "[data-testid=track-action-error]";

    private IRenderedComponent<TrackDetail> RenderPage(params TrackDto[] states)
    {
        Api.GetTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(states[0], states[1..]);
        return Render<TrackDetail>(p => p.Add(c => c.Id, TestData.TrackId));
    }

    [Fact]
    public async Task Retry_WhileRunning_IsBusyDisabled_AndIgnoresSecondClick()
    {
        var pending = new TaskCompletionSource<TrackDto>();
        Api.RetryTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(pending.Task);
        var page = RenderPage(TestData.FailedTrack());

        var first = page.Find(RetryButton).ClickAsync(new());

        page.Find(RetryButton).HasAttribute("disabled").ShouldBeTrue();
        page.Find(RetryButton).TextContent.ShouldContain("Retrying...");
        await page.Find(RetryButton).ClickAsync(new()); // RadzenButton ignores re-entrant clicks
        await Api.Received(1).RetryTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>());

        pending.SetResult(TestData.Track(TrackStatusDto.Queued));
        await first;
        page.Find("[data-testid=track-status]").TextContent.ShouldBe("Queued");
        page.FindAll(RetryButton).ShouldBeEmpty();
    }

    [Fact]
    public async Task Retry_EndingFailedAgain_ShowsMessage_ClearedByNextRetry()
    {
        var pending = new TaskCompletionSource<TrackDto>();
        Api.RetryTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TestData.Track(TrackStatusDto.Queued)), pending.Task);
        var page = RenderPage(TestData.FailedTrack(), TestData.FailedTrack());
        page.FindAll(FailedAgain).ShouldBeEmpty();

        await page.Find(RetryButton).ClickAsync(new());
        Timer.Tick();

        page.WaitForAssertion(() => page.Find(FailedAgain).TextContent.ShouldBe("The retry failed again."));
        var second = page.Find(RetryButton).ClickAsync(new());
        page.FindAll(FailedAgain).ShouldBeEmpty();
        pending.SetResult(TestData.Track(TrackStatusDto.Queued));
        await second;
    }

    [Fact]
    public async Task RetryRejected_ShowsActionError_WithoutFailedAgainMessage()
    {
        Api.RetryTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>())
            .ThrowsAsync(Problem(HttpStatusCode.ServiceUnavailable, "The worker is not available."));
        var page = RenderPage(TestData.FailedTrack());

        await page.Find(RetryButton).ClickAsync(new());

        page.Find(ActionError).TextContent.ShouldBe("The worker is not available.");
        page.FindAll(FailedAgain).ShouldBeEmpty();
        page.Find(RetryButton).HasAttribute("disabled").ShouldBeFalse();
    }

    [Theory]
    [InlineData(TrackStatusDto.Failed, true)]
    [InlineData(TrackStatusDto.Completed, true)]
    [InlineData(TrackStatusDto.Queued, false)]
    [InlineData(TrackStatusDto.Separating, false)]
    public void DeleteButton_IsShownOnlyForTerminalTracks(TrackStatusDto status, bool shown)
    {
        var page = RenderPage(TestData.Track(status));

        (page.FindAll(DeleteButton).Count == 1).ShouldBe(shown);
        page.FindAll(DeleteConfirm).ShouldBeEmpty();
    }

    [Fact]
    public async Task Delete_AsksForConfirmation_AndCancelKeepsTheTrack()
    {
        var page = RenderPage(TestData.Track());

        await page.Find(DeleteButton).ClickAsync(new());

        page.Find(".ss-delete").TextContent.ShouldContain("track, its stems and all of its mixes");
        page.FindAll(DeleteButton).ShouldBeEmpty();
        await page.Find(DeleteCancel).ClickAsync(new());
        page.FindAll(DeleteConfirm).ShouldBeEmpty();
        page.Find(DeleteButton).ShouldNotBeNull();
        await Api.DidNotReceive().DeleteTrackAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteConfirmed_DeletesAndNavigatesToTrackList()
    {
        var page = RenderPage(TestData.FailedTrack());

        await page.Find(DeleteButton).ClickAsync(new());
        await page.Find(DeleteConfirm).ClickAsync(new());

        await Api.Received(1).DeleteTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>());
        NavigatedTo.ShouldBe("http://localhost/tracks");
    }

    [Fact]
    public async Task DeleteWhileRunning_IsBusy_AndIgnoresSecondClick()
    {
        var pending = new TaskCompletionSource();
        Api.DeleteTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(pending.Task);
        var page = RenderPage(TestData.Track());
        await page.Find(DeleteButton).ClickAsync(new());

        var first = page.Find(DeleteConfirm).ClickAsync(new());

        page.Find(DeleteConfirm).HasAttribute("disabled").ShouldBeTrue();
        page.Find(DeleteConfirm).TextContent.ShouldContain("Deleting...");
        await page.Find(DeleteConfirm).ClickAsync(new());
        await Api.Received(1).DeleteTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>());
        pending.SetResult();
        await first;
    }

    [Fact]
    public async Task DeleteRejected_ShowsActionError_AndClosesConfirmation()
    {
        Api.DeleteTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>())
            .ThrowsAsync(Problem(HttpStatusCode.Conflict, "Track is busy", "The track is still processing."));
        var page = RenderPage(TestData.Track());

        await page.Find(DeleteButton).ClickAsync(new());
        await page.Find(DeleteConfirm).ClickAsync(new());

        page.Find(ActionError).TextContent.ShouldBe("The track is still processing.");
        page.FindAll(DeleteConfirm).ShouldBeEmpty();
        NavigatedTo.ShouldBe("http://localhost/");
    }
}
