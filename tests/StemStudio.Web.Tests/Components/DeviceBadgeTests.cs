using Bunit;
using StemStudio.Contracts.Health;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Components.Shared;
using StemStudio.Web.Services;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Components;

/// <summary>Device badge and its display mapping (FR-006, US1-1, ui-test-ids 1.2.0).</summary>
public sealed class DeviceBadgeTests : PageTestContext
{
    private const string Gpu = "NVIDIA GeForce RTX 5070 Ti";
    private const string Reason = "Not enough free GPU memory (needs 10.6 GB, 4.2 GB free).";

    [Fact]
    public void FromJob_Gpu_UsesDisplayText()
    {
        var display = DeviceDisplay.FromJob(new JobDeviceDto(ProcessingDeviceKindDto.Gpu, Gpu, false, null));

        display.ShouldBe(new DeviceDisplay("gpu", $"GPU ({Gpu})", false, null));
    }

    [Fact]
    public void FromJob_CpuFallback_KeepsTheReasonVerbatim()
    {
        var display = DeviceDisplay.FromJob(new JobDeviceDto(ProcessingDeviceKindDto.Gpu, Gpu, true, Reason));

        display.ShouldBe(new DeviceDisplay("gpu", $"GPU ({Gpu})", true, Reason));
    }

    [Fact]
    public void FromJob_ReasonWithoutFallback_IsHidden()
    {
        var display = DeviceDisplay.FromJob(new JobDeviceDto(ProcessingDeviceKindDto.Cpu, null, false, Reason));

        display.ShouldBe(new DeviceDisplay("cpu", "CPU", false, null));
    }

    [Fact]
    public void FromJob_FallbackWithoutReason_ShowsAGenericReason()
    {
        var display = DeviceDisplay.FromJob(new JobDeviceDto(ProcessingDeviceKindDto.Cpu, null, true, null));

        display.Reason.ShouldBe(DeviceDisplay.UnknownFallbackReason);
    }

    [Fact]
    public void FromWorker_NoAnswer_IsUnknown()
    {
        DeviceDisplay.FromWorker(null).ShouldBe(DeviceDisplay.Unknown);
        DeviceDisplay.Unknown.ShouldBe(new DeviceDisplay("unknown", "Unknown", null, null));
    }

    [Fact]
    public void FromWorker_ReachableWithoutKind_IsUnknown()
    {
        DeviceDisplay.FromWorker(new ProcessingDeviceDto { Reachable = true }).ShouldBe(DeviceDisplay.Unknown);
    }

    [Fact]
    public void FromWorker_Gpu_HasNoFallbackFlag()
    {
        var display = DeviceDisplay.FromWorker(new ProcessingDeviceDto { Reachable = true, Kind = ProcessingDeviceKindDto.Gpu, Name = Gpu });

        display.ShouldBe(new DeviceDisplay("gpu", $"GPU ({Gpu})", null, null));
    }

    [Fact]
    public void Badge_RendersTextAndAttributes()
    {
        var badge = RenderBadge(new DeviceDisplay("gpu", $"GPU ({Gpu})", false, null));

        var device = badge.Find("[data-testid=x-device]");
        device.TextContent.ShouldBe($"GPU ({Gpu})");
        device.GetAttribute("data-device").ShouldBe("gpu");
        device.GetAttribute("data-fallback").ShouldBe("false");
        badge.FindAll("[data-testid=x-reason]").ShouldBeEmpty();
    }

    [Fact]
    public void Badge_WithReason_ShowsTheReason()
    {
        var badge = RenderBadge(new DeviceDisplay("cpu", "CPU", true, Reason));

        badge.Find("[data-testid=x-device]").GetAttribute("data-fallback").ShouldBe("true");
        badge.Find("[data-testid=x-reason]").TextContent.ShouldBe(Reason);
    }

    [Fact]
    public void Badge_WithoutFallbackFlag_LeavesTheAttributeOut()
    {
        var badge = RenderBadge(new DeviceDisplay("unknown", "Unknown", null, null));

        badge.Find("[data-testid=x-device]").HasAttribute("data-fallback").ShouldBeFalse();
    }

    [Fact]
    public void Badge_HasAVisibleLabel_SoTheDeviceIsNotConveyedByColour()
    {
        var badge = RenderBadge(new DeviceDisplay("gpu", $"GPU ({Gpu})", false, null));

        badge.Find(".ss-device-label").TextContent.ShouldBe("Device:");
        badge.Find(".ss-device").TextContent.Trim().ShouldStartWith($"Device: GPU ({Gpu})");
    }

    [Fact]
    public void Badge_CustomLabel_IsShown()
    {
        var badge = RenderBadge(new DeviceDisplay("cpu", "CPU", false, null), "Processed on");

        badge.Find(".ss-device-label").TextContent.ShouldBe("Processed on:");
    }

    [Fact]
    public void JobFallback_IsSpelledOut_AndTheReasonStaysVerbatim()
    {
        var badge = RenderBadge(new DeviceDisplay("cpu", "CPU", true, Reason));

        badge.Find(".ss-device-reason-label").TextContent.ShouldBe("CPU fallback:");
        badge.Find("[data-testid=x-reason]").TextContent.ShouldBe(Reason);
        badge.Find(".ss-device-reason").GetAttribute("role").ShouldBe("note");
    }

    [Fact]
    public void WorkerReason_SaysTheGpuIsNotUsed()
    {
        var badge = RenderBadge(new DeviceDisplay("cpu", "CPU", null, Reason));

        badge.Find(".ss-device-reason-label").TextContent.ShouldBe("GPU not used:");
    }

    private IRenderedComponent<DeviceBadge> RenderBadge(DeviceDisplay display, string? label = null) => Render<DeviceBadge>(p =>
    {
        p.Add(c => c.Display, display).Add(c => c.TestId, "x-device").Add(c => c.ReasonTestId, "x-reason");
        if (label is not null)
        {
            p.Add(c => c.Label, label);
        }
    });
}
