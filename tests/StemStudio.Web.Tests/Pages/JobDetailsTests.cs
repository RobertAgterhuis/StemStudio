using Bunit;
using Microsoft.AspNetCore.Components;
using NSubstitute;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Components.Pages;
using StemStudio.Web.Experience;
using StemStudio.Web.Preferences;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Pages;

/// <summary>Expert job details on the track page (T060, FR-017).</summary>
public sealed class JobDetailsTests : PageTestContext
{
    private IRenderedComponent<CascadingValue<ExperienceContext>> RenderAt(ExperienceLevel level, TrackDto track)
    {
        Api.GetTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(track);
        return Render<CascadingValue<ExperienceContext>>(p => p
            .Add(c => c.Value, new ExperienceContext(level, DensityPreference.Comfortable))
            .AddChildContent<TrackDetail>(d => d.Add(x => x.Id, TestData.TrackId)));
    }

    private static TrackDto WithDevices() => TestData.DetailTrack() with
    {
        Device = new JobDeviceDto(ProcessingDeviceKindDto.Cpu, null, true, "CUDA out of memory"),
        Detail = TestData.CompletedDetail() with { Device = new JobDeviceDto(ProcessingDeviceKindDto.Gpu, "RTX", false, null) },
    };

    [Fact]
    public void Expert_HasACollapsedDisclosure()
    {
        var page = RenderAt(ExperienceLevel.Expert, WithDevices());

        var button = page.Find("[data-testid=job-details]");
        button.GetAttribute("aria-expanded").ShouldBe("false");
        button.GetAttribute("data-help-id").ShouldBe("track-job-details");
        page.FindAll("[data-testid=job-details-standard]").ShouldBeEmpty();
    }

    [Fact]
    public void Opening_ShowsModelStageDeviceAndFallbackOfBothJobs()
    {
        var page = RenderAt(ExperienceLevel.Expert, WithDevices());

        page.Find("[data-testid=job-details]").Click();

        page.Find("[data-testid=job-details]").GetAttribute("aria-expanded").ShouldBe("true");
        var standard = page.Find("[data-testid=job-details-standard]").TextContent;
        standard.ShouldContain("htdemucs_6s");
        standard.ShouldContain("Completed");
        standard.ShouldContain("CPU");
        standard.ShouldContain("CUDA out of memory");
        var detail = page.Find("[data-testid=job-details-detail]").TextContent;
        detail.ShouldContain("completed");
        detail.ShouldContain("GPU (RTX)");
    }

    [Fact]
    public void WithoutDevices_ShowsNotRecorded()
    {
        var page = RenderAt(ExperienceLevel.Expert, TestData.Track());

        page.Find("[data-testid=job-details]").Click();

        page.Find("[data-testid=job-details-standard]").TextContent.ShouldContain("not recorded");
        page.Find("[data-testid=job-details-detail]").TextContent.ShouldContain("none");
    }

    [Fact]
    public void Advanced_HasNoJobDetails()
    {
        RenderAt(ExperienceLevel.Advanced, WithDevices()).FindAll("[data-testid=job-details]").ShouldBeEmpty();
    }

    [Fact]
    public void CompletedTrack_RegistersTheOpenMixerAction()
    {
        RenderAt(ExperienceLevel.Advanced, TestData.Track());

        var action = Services.GetService<StemStudio.Web.Services.PageCommands>()!.Items.Single(i => i.Id == "open-mixer");
        action.Label.ShouldBe("Open mixer");
    }
}
