using System.Net;
using Bunit;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Components.Pages;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Pages;

public sealed class TrackListPageTests : PageTestContext
{
    private static readonly Guid RunningId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    [Fact]
    public void Rows_ShowTitleArtistStatusDuration_AndLinks()
    {
        var running = TestData.Track(TrackStatusDto.Separating) with { Id = RunningId, Artist = null, Title = "Upload" };
        Api.ListTracksAsync(Arg.Any<CancellationToken>()).Returns([TestData.Track(), running]);

        var page = Render<Tracks>();

        var row = page.Find($"[data-testid=track-row-{TestData.TrackId}]");
        row.TextContent.ShouldContain("Never Gonna Give You Up");
        row.TextContent.ShouldContain("Rick Astley");
        row.TextContent.ShouldContain("3:33");
        page.Find($"[data-testid=track-row-status-{TestData.TrackId}]").TextContent.ShouldBe("Completed");
        row.QuerySelector("a[data-testid=track-link]")!.GetAttribute("href").ShouldBe($"tracks/{TestData.TrackId}");
        row.QuerySelector("a[data-testid=track-mixer-link]")!.GetAttribute("href").ShouldBe($"tracks/{TestData.TrackId}/mixer");

        var runningRow = page.Find($"[data-testid=track-row-{RunningId}]");
        page.Find($"[data-testid=track-row-status-{RunningId}]").TextContent.ShouldBe("Separating");
        runningRow.QuerySelector("a[data-testid=track-mixer-link]").ShouldBeNull();
        runningRow.TextContent.ShouldContain("-");
    }

    [Fact]
    public void Empty_ShowsHint()
    {
        Api.ListTracksAsync(Arg.Any<CancellationToken>()).Returns([]);

        var page = Render<Tracks>();

        page.Find("[data-testid=tracks-empty]").TextContent.ShouldContain("No tracks yet");
    }

    [Fact]
    public void ApiError_IsShown()
    {
        Api.ListTracksAsync(Arg.Any<CancellationToken>()).ThrowsAsync(Problem(HttpStatusCode.ServiceUnavailable, "The StemStudio API cannot be reached. Is it running?"));

        var page = Render<Tracks>();

        page.Find("[data-testid=tracks-error]").TextContent.ShouldBe("The StemStudio API cannot be reached. Is it running?");
    }

    [Fact]
    public void FailedStatus_UsesDangerBadge()
    {
        Api.ListTracksAsync(Arg.Any<CancellationToken>()).Returns([TestData.FailedTrack()]);

        var page = Render<Tracks>();

        page.Find($"[data-testid=track-row-status-{TestData.TrackId}]").ClassList.ShouldContain("rz-badge-danger");
    }
}
