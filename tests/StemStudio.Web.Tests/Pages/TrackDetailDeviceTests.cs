using Bunit;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Pages;

/// <summary>Device of the standard and the detail job on the track page (US1-1, US3-1, FR-006).</summary>
public sealed class TrackDetailDeviceTests : TrackDetailTestContext
{
    private const string Gpu = "NVIDIA GeForce RTX 5070 Ti";
    private const string Reason = "GPU out of memory; the step ran on the CPU.";

    private static readonly JobDeviceDto GpuDevice = new(ProcessingDeviceKindDto.Gpu, Gpu, false, null);
    private static readonly JobDeviceDto FallbackDevice = new(ProcessingDeviceKindDto.Cpu, null, true, Reason);

    [Fact]
    public void TrackDevice_IsShown_WhileProcessing()
    {
        var page = RenderPage(TestData.Track(TrackStatusDto.Separating) with { Device = GpuDevice });

        var device = page.Find("[data-testid=track-device]");
        device.TextContent.ShouldBe($"GPU ({Gpu})");
        device.GetAttribute("data-device").ShouldBe("gpu");
        device.GetAttribute("data-fallback").ShouldBe("false");
        page.FindAll("[data-testid=track-device-fallback]").ShouldBeEmpty();
    }

    [Fact]
    public void TrackDevice_Fallback_ShowsTheReason()
    {
        var page = RenderPage(TestData.Track() with { Device = FallbackDevice });

        page.Find("[data-testid=track-device]").GetAttribute("data-device").ShouldBe("cpu");
        page.Find("[data-testid=track-device-fallback]").TextContent.ShouldBe(Reason);
    }

    [Fact]
    public void NoDevice_ShowsNoBadge()
    {
        var page = RenderPage(TestData.DetailTrack());

        page.FindAll("[data-testid=track-device]").ShouldBeEmpty();
        page.FindAll("[data-testid=detail-device]").ShouldBeEmpty();
        page.FindAll("[data-testid=track-device-fallback]").ShouldBeEmpty();
        page.FindAll("[data-testid=detail-device-fallback]").ShouldBeEmpty();
    }

    [Fact]
    public void DetailDevice_IsShownInsideTheDetailSection()
    {
        var detail = TestData.CompletedDetail() with { Device = FallbackDevice };
        var page = RenderPage(TestData.Track() with { Device = GpuDevice, Detail = detail });

        var section = page.Find("[data-testid=detail-section]");
        var device = section.QuerySelector("[data-testid=detail-device]").ShouldNotBeNull();
        device.TextContent.ShouldBe("CPU");
        device.GetAttribute("data-fallback").ShouldBe("true");
        section.QuerySelector("[data-testid=detail-device-fallback]").ShouldNotBeNull().TextContent.ShouldBe(Reason);
        section.QuerySelector("[data-testid=track-device]").ShouldBeNull();
    }

    [Fact]
    public void Badges_AreLabelledPerJob()
    {
        var detail = TestData.CompletedDetail() with { Device = GpuDevice };
        var page = RenderPage(TestData.Track() with { Device = GpuDevice, Detail = detail });

        var labels = page.FindAll(".ss-device-label").Select(l => l.TextContent).ToList();
        labels.ShouldBe(["Processed on:", "Detail stems processed on:"]);
    }

    [Fact]
    public void DetailDevice_WithoutFallback_HasNoReason()
    {
        var detail = TestData.Phase(DetailPhaseStatusDto.Running, 30, "Splitting drums") with { Device = GpuDevice };
        var page = RenderPage(TestData.WithDetail(detail));

        page.Find("[data-testid=detail-device]").GetAttribute("data-device").ShouldBe("gpu");
        page.FindAll("[data-testid=detail-device-fallback]").ShouldBeEmpty();
    }
}
