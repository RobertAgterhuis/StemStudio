using System.Net;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StemStudio.Contracts.Generation;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Components.Generation;
using StemStudio.Web.Components.Pages;
using StemStudio.Web.Components.Styles;
using StemStudio.Web.Generation;
using StemStudio.Web.Help;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Generation;

/// <summary>Clear behaviour without a GPU (009 T055, US6-1/2, FR-010; ui-test-ids 1.8.0 generation-status*).</summary>
public sealed class GenerationStatusTests : PageTestContext
{
    private const string NoGpu = "No NVIDIA GPU is available to the generator (CUDA is not available).";

    private IRenderedComponent<TrackDetail> RenderTrackPage(TrackDto track)
    {
        Api.GetTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(track);
        return Render<TrackDetail>(p => p.Add(c => c.Id, TestData.TrackId));
    }

    [Fact]
    public void NotInstalled_ShowsTheStateTheReasonAndTheHelpLink()
    {
        var banner = Render<GenerationStatusBanner>();

        var status = banner.Find("[data-testid=generation-status]");
        (status.GetAttribute("data-state"), status.GetAttribute("role"), status.GetAttribute("data-help-id"))
            .ShouldBe(("notInstalled", "status", "generation-gpu-required"));
        var reason = banner.Find("[data-testid=generation-status-reason]");
        (reason.Id, reason.TextContent).ShouldBe(("generation-status-reason", "The generator service is not installed."));
        banner.Find("[data-testid=generation-help-link]").TextContent.ShouldContain("needs a GPU");
        banner.FindAll("[data-testid=generation-download-notice]").ShouldBeEmpty();
    }

    [Fact]
    public void Unavailable_ShowsTheProbeReason()
    {
        Api.GetGenerationStatusAsync(Arg.Any<CancellationToken>()).Returns(GenerationData.Status(GenerationAvailabilityDto.Unavailable, NoGpu));

        var banner = Render<GenerationStatusBanner>();

        banner.Find("[data-testid=generation-status]").GetAttribute("data-state").ShouldBe("unavailable");
        banner.Find("[data-testid=generation-status-reason]").TextContent.ShouldBe(NoGpu);
    }

    [Fact]
    public void ANotReadyStateWithoutReason_GetsADefaultText()
    {
        Api.GetGenerationStatusAsync(Arg.Any<CancellationToken>()).Returns(GenerationData.Status(GenerationAvailabilityDto.Unavailable));

        var banner = Render<GenerationStatusBanner>();

        banner.Find("[data-testid=generation-status-reason]").TextContent.ShouldBe("Generation is not available on this computer.");
    }

    [Fact]
    public void Ready_HasNoReason_AndAnnouncesTheFirstDownload()
    {
        Api.GetGenerationStatusAsync(Arg.Any<CancellationToken>()).Returns(GenerationData.Status() with { DownloadBytesNeeded = 4_800_000_000 });

        var banner = Render<GenerationStatusBanner>();

        banner.Find("[data-testid=generation-status]").GetAttribute("data-state").ShouldBe("ready");
        banner.FindAll("[data-testid=generation-status-reason]").ShouldBeEmpty();
        banner.Find("[data-testid=generation-download-notice]").TextContent.ShouldBe("First use downloads about 4.5 GB of model files.");
    }

    [Fact]
    public void Ready_WithEverythingDownloaded_ShowsNoNotice()
    {
        Api.GetGenerationStatusAsync(Arg.Any<CancellationToken>()).Returns(GenerationData.Status());

        var banner = Render<GenerationStatusBanner>();

        banner.FindAll("[data-testid=generation-download-notice]").ShouldBeEmpty();
        banner.Find("[data-testid=generation-status]").TextContent.ShouldContain("Generation is ready");
    }

    [Fact]
    public async Task HelpLink_OpensTheGpuTopic()
    {
        var banner = Render<GenerationStatusBanner>();

        await banner.Find("[data-testid=generation-help-link]").ClickAsync(new());

        var help = Services.GetRequiredService<HelpContext>();
        (help.IsOpen, help.TopicId).ShouldBe((true, "generation-gpu-required"));
    }

    [Fact]
    public void TrackPage_DisablesRestyle_WithTheReason_WhenNotInstalled()
    {
        var page = RenderTrackPage(TestData.Track());

        var button = page.Find("[data-testid=track-restyle]");
        button.HasAttribute("disabled").ShouldBeTrue();
        button.GetAttribute("aria-describedby").ShouldBe("generation-status-reason");
        page.Find("[data-testid=track-generations] [data-testid=generation-status]").GetAttribute("data-state").ShouldBe("notInstalled");
    }

    [Fact]
    public void TrackPage_DisablesRestyle_ForATrackThatIsNotCompleted()
    {
        Api.GetGenerationStatusAsync(Arg.Any<CancellationToken>()).Returns(GenerationData.Status());

        var page = RenderTrackPage(TestData.Track(TrackStatusDto.Separating));

        var button = page.Find("[data-testid=track-restyle]");
        button.HasAttribute("disabled").ShouldBeTrue();
        button.GetAttribute("aria-describedby").ShouldBe("track-restyle-reason");
        page.Find("#track-restyle-reason").TextContent.ShouldBe("Restyle is possible once the track is separated.");
    }

    [Fact]
    public void StylesAndGenerationsPages_ShowTheBanner_AndStylesStayUsable()
    {
        var styles = Render<StylesPage>();
        styles.Find("[data-testid=generation-status]").GetAttribute("data-state").ShouldBe("notInstalled");
        styles.FindAll("[data-testid^=style-row-]").Count.ShouldBe(3);

        var generations = Render<Generations>();
        generations.Find("[data-testid=generation-status]").GetAttribute("data-state").ShouldBe("notInstalled");
    }

    [Fact]
    public async Task StatusSource_CachesForTenSeconds_AndTurnsFailuresIntoNotInstalled()
    {
        Api.GetGenerationStatusAsync(Arg.Any<CancellationToken>())
            .Returns(_ => GenerationData.Status(), _ => throw Problem(HttpStatusCode.ServiceUnavailable, "The StemStudio API cannot be reached. Is it running?"));
        var source = new GenerationStatusSource(Api, Time);

        (await source.GetAsync(TestContext.Current.CancellationToken)).State.ShouldBe(GenerationAvailabilityDto.Ready);
        Time.Advance(TimeSpan.FromSeconds(9));
        (await source.GetAsync(TestContext.Current.CancellationToken)).State.ShouldBe(GenerationAvailabilityDto.Ready);
        Time.Advance(TimeSpan.FromSeconds(1));
        var failed = await source.GetAsync(TestContext.Current.CancellationToken);

        (failed.State, failed.Reason).ShouldBe((GenerationAvailabilityDto.NotInstalled, "The generation status could not be read: The StemStudio API cannot be reached. Is it running?"));
        await Api.Received(2).GetGenerationStatusAsync(Arg.Any<CancellationToken>());
    }
}
