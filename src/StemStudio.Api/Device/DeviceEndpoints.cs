using StemStudio.Contracts;
using StemStudio.Contracts.Health;

namespace StemStudio.Api.Device;

/// <summary>Maps <c>GET /api/device</c> (backend contract 1.3.0 <c>getProcessingDevice</c>).</summary>
public static class DeviceEndpoints
{
    /// <summary>Maps the device endpoint; it always answers 200 (<c>reachable: false</c> when the worker is not reachable).</summary>
    /// <param name="app">Endpoint builder.</param>
    /// <returns>The builder.</returns>
    public static IEndpointRouteBuilder MapDeviceEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRoutes.Device, (ProcessingDeviceService devices, CancellationToken ct) => devices.GetAsync(ct))
            .WithName("getProcessingDevice").WithTags("health").Produces<ProcessingDeviceDto>();
        return app;
    }
}
