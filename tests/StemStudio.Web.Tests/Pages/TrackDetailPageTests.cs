using System.Net;
using Bunit;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Contracts;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Components.Pages;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Pages;

public sealed class TrackDetailPageTests : PageTestContext
{
    private IRenderedComponent<TrackDetail> RenderPage() =>
        Render<TrackDetail>(p => p.Add(c => c.Id, TestData.TrackId));

    [Fact]
    public void Running_ShowsProgressAndStage_ThenPollsUntilCompleted()
    {
        Api.GetTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>())
            .Returns(TestData.Track(TrackStatusDto.Separating), TestData.Track());
        var page = RenderPage();

        page.Find("[data-testid=track-title]").TextContent.ShouldBe("Never Gonna Give You Up");
        page.Find("[data-testid=track-status]").TextContent.ShouldBe("Separating");
        page.Find("[data-testid=track-stage]").TextContent.ShouldBe("Separating stems");
        page.Find("[data-testid=track-progress]").GetAttribute("aria-valuenow").ShouldBe("47");
        page.FindAll("[data-testid=open-mixer]").ShouldBeEmpty();

        Timer.Tick();

        page.WaitForAssertion(() => page.Find("[data-testid=track-status]").TextContent.ShouldBe("Completed"));
        page.Find("[data-testid=track-progress]").GetAttribute("aria-valuenow").ShouldBe("100");
        Timer.Waits.ShouldBe(1);
    }

    [Fact]
    public void Completed_ShowsStemsBpmDurationModelAndMixerLink_WithoutPolling()
    {
        Api.GetTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(TestData.Track());
        var page = RenderPage();

        page.Find("[data-testid=track-bpm]").TextContent.ShouldBe("113.5 BPM");
        page.Find("[data-testid=track-duration]").TextContent.ShouldBe("3:33");
        page.Find("[data-testid=track-model]").TextContent.ShouldBe("htdemucs_6s (6 stems)");
        foreach (var stem in StemNames.SixStem)
        {
            page.Find($"[data-testid=track-stem-{stem}]").TextContent.ShouldBe(stem);
        }

        page.Find("[data-testid=open-mixer]").GetAttribute("href").ShouldBe($"tracks/{TestData.TrackId}/mixer");
        page.FindAll("[data-testid=track-fallback-note]").ShouldBeEmpty();
        Timer.Waits.ShouldBe(0);
    }

    [Fact]
    public void FourStemFallback_ShowsNote()
    {
        Api.GetTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(TestData.Track(model: "htdemucs"));
        var page = RenderPage();

        page.Find("[data-testid=track-model]").TextContent.ShouldBe("htdemucs (4 stems)");
        page.Find("[data-testid=track-fallback-note]").TextContent.ShouldContain("4-stem fallback model");
        page.FindAll("[data-testid=track-stem-guitar]").ShouldBeEmpty();
    }

    [Fact]
    public void UnreliableOrMissingGrid_IsExplained()
    {
        Api.GetTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>())
            .Returns(TestData.Track() with { BeatGrid = null, DurationSeconds = null });
        var page = RenderPage();

        page.Find("[data-testid=track-bpm]").TextContent.ShouldBe("not detected");
        page.Find("[data-testid=track-duration]").TextContent.ShouldBe("-");
    }

    [Fact]
    public async Task Failed_ShowsReason_AndRetryRestartsPolling()
    {
        Api.GetTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(TestData.FailedTrack(), TestData.Track());
        Api.RetryTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(TestData.Track(TrackStatusDto.Queued));
        var page = RenderPage();

        page.Find("[data-testid=track-error]").TextContent.ShouldBe("No matching YouTube audio was found for this Spotify track.");
        page.Find("[data-testid=track-status]").TextContent.ShouldBe("Failed");
        Timer.Waits.ShouldBe(0);

        await page.Find("[data-testid=track-retry]").ClickAsync(new());

        page.Find("[data-testid=track-status]").TextContent.ShouldBe("Queued");
        page.FindAll("[data-testid=track-error]").ShouldBeEmpty();
        Timer.Tick();
        page.WaitForAssertion(() => page.Find("[data-testid=track-status]").TextContent.ShouldBe("Completed"));
    }

    [Fact]
    public async Task RetryConflict_ShowsActionError()
    {
        Api.GetTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(TestData.FailedTrack());
        Api.RetryTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>())
            .ThrowsAsync(Problem(HttpStatusCode.Conflict, "Track is not failed", "The uploaded file is no longer available; import it again."));
        var page = RenderPage();

        await page.Find("[data-testid=track-retry]").ClickAsync(new());

        page.Find("[data-testid=track-action-error]").TextContent.ShouldBe("The uploaded file is no longer available; import it again.");
    }

    [Fact]
    public void NotFound_ShowsMessage()
    {
        Api.GetTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>()).ThrowsAsync(Problem(HttpStatusCode.NotFound, "Not found", "Track does not exist."));
        var page = RenderPage();

        page.Find("[data-testid=track-load-error]").TextContent.ShouldBe("Track does not exist.");
        page.FindAll("[data-testid=track-title]").ShouldBeEmpty();
    }

    [Fact]
    public void PollingError_IsShown_AndPollingContinues()
    {
        Api.GetTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(
            _ => TestData.Track(TrackStatusDto.Downloading),
            _ => throw Problem(HttpStatusCode.ServiceUnavailable, "The StemStudio API cannot be reached. Is it running?"),
            _ => TestData.Track());
        var page = RenderPage();

        Timer.Tick();
        page.WaitForAssertion(() => page.Find("[data-testid=track-action-error]").TextContent.ShouldContain("cannot be reached"));
        page.Find("[data-testid=track-status]").TextContent.ShouldBe("Downloading");

        Timer.Tick();
        page.WaitForAssertion(() => page.Find("[data-testid=track-status]").TextContent.ShouldBe("Completed"));
        page.FindAll("[data-testid=track-action-error]").ShouldBeEmpty();
    }

    [Fact]
    public void Dispose_StopsPolling()
    {
        Api.GetTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(TestData.Track(TrackStatusDto.Analysing));
        var page = RenderPage();

        page.Instance.Dispose();
        Timer.Tick();

        Api.Received(1).GetTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>());
    }
}
