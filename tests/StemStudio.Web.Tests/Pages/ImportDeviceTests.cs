using System.Net;
using Bunit;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Contracts.Health;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Components.Pages;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Pages;

/// <summary>The worker's processing device on the import page (US3-1, FR-006, ui-test-ids 1.2.0).</summary>
public sealed class ImportDeviceTests : PageTestContext
{
    private const string Device = "[data-testid=worker-device]";
    private const string Reason = "[data-testid=worker-device-reason]";
    private const string NoGpu = "No usable NVIDIA GPU: CUDA is not available in this container.";

    [Fact]
    public void Gpu_ShowsItsName()
    {
        var page = RenderWith(new ProcessingDeviceDto
        {
            Reachable = true,
            Kind = ProcessingDeviceKindDto.Gpu,
            Name = "NVIDIA GeForce RTX 5070 Ti",
            TotalMemoryMiB = 16303,
        });

        var device = page.Find(Device);
        device.GetAttribute("data-device").ShouldBe("gpu");
        device.TextContent.ShouldBe("GPU (NVIDIA GeForce RTX 5070 Ti)");
        page.FindAll(Reason).ShouldBeEmpty();
    }

    [Fact]
    public void Cpu_WithReason_ShowsTheReason()
    {
        var page = RenderWith(new ProcessingDeviceDto { Reachable = true, Kind = ProcessingDeviceKindDto.Cpu, Reason = NoGpu });

        page.Find(Device).GetAttribute("data-device").ShouldBe("cpu");
        page.Find(Device).TextContent.ShouldBe("CPU");
        page.Find(Reason).TextContent.ShouldBe(NoGpu);
    }

    [Fact]
    public void Cpu_WithoutReason_HasNoReason()
    {
        var page = RenderWith(new ProcessingDeviceDto { Reachable = true, Kind = ProcessingDeviceKindDto.Cpu });

        page.Find(Device).GetAttribute("data-device").ShouldBe("cpu");
        page.FindAll(Reason).ShouldBeEmpty();
    }

    [Fact]
    public void Unreachable_ShowsUnknown()
    {
        var page = RenderWith(ProcessingDeviceDto.Unreachable);

        page.Find(Device).GetAttribute("data-device").ShouldBe("unknown");
        page.Find(Device).TextContent.ShouldBe("Unknown");
        page.Find(Device).HasAttribute("data-fallback").ShouldBeFalse();
        page.FindAll(Reason).ShouldBeEmpty();
    }

    [Fact]
    public void ApiError_ShowsUnknown_AndKeepsTheImportUsable()
    {
        Api.GetDeviceAsync(Arg.Any<CancellationToken>()).ThrowsAsync(Problem(HttpStatusCode.InternalServerError, "Boom"));

        var page = Render<Import>();

        page.Find(Device).GetAttribute("data-device").ShouldBe("unknown");
        page.FindAll("[data-testid=import-error]").ShouldBeEmpty();
        page.Find("[data-testid=import-url-submit]").HasAttribute("disabled").ShouldBeFalse();
    }

    private IRenderedComponent<Import> RenderWith(ProcessingDeviceDto device)
    {
        Api.GetDeviceAsync(Arg.Any<CancellationToken>()).Returns(device);
        return Render<Import>();
    }
}
