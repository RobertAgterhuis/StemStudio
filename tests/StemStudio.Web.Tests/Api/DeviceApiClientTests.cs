using System.Net;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Api;

/// <summary>Device operation and device fields of the typed client (backend contract 1.3.0, FR-006).</summary>
public sealed partial class StemStudioApiClientTests
{
    private const string GpuDeviceJson = """
        { "reachable": true, "kind": "gpu", "name": "NVIDIA GeForce RTX 5070 Ti", "totalMemoryMiB": 16303, "reason": null }
        """;

    private const string UnreachableDeviceJson = """
        { "reachable": false, "kind": null, "name": null, "totalMemoryMiB": null, "reason": null }
        """;

    [Fact]
    public async Task GetDevice_GetsDeviceRoute_AndReadsTheGpu()
    {
        _handler.Respond(HttpStatusCode.OK, GpuDeviceJson);

        var device = await _client.GetDeviceAsync(Ct);

        device.Reachable.ShouldBeTrue();
        device.Kind.ShouldBe(ProcessingDeviceKindDto.Gpu);
        device.Name.ShouldBe("NVIDIA GeForce RTX 5070 Ti");
        device.TotalMemoryMiB.ShouldBe(16303);
        var request = _handler.Requests.ShouldHaveSingleItem();
        request.Method.ShouldBe(HttpMethod.Get);
        request.PathAndQuery.ShouldBe("/api/device");
    }

    [Fact]
    public async Task GetDevice_Unreachable_HasNoKind()
    {
        _handler.Respond(HttpStatusCode.OK, UnreachableDeviceJson);

        var device = await _client.GetDeviceAsync(Ct);

        device.Reachable.ShouldBeFalse();
        device.Kind.ShouldBeNull();
    }

    [Fact]
    public async Task GetTrack_ReadsTheJobDevice()
    {
        const string device = """
            "device": { "kind": "cpu", "name": null, "cpuFallback": true, "fallbackReason": "Out of GPU memory." }, "status":
            """;
        var json = TestData.CompletedTrackJson.Replace("\"status\":", device, StringComparison.Ordinal);
        _handler.Respond(HttpStatusCode.OK, json);

        var track = await _client.GetTrackAsync(TestData.TrackId, Ct);

        track.Device.ShouldBe(new JobDeviceDto(ProcessingDeviceKindDto.Cpu, null, true, "Out of GPU memory."));
    }
}
