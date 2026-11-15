using StemStudio.Api.Generator;
using StemStudio.Contracts.Generation;
using StemStudio.Contracts.Health;
using StemStudio.Contracts.Tracks;

namespace StemStudio.Api.Generations;

/// <summary><c>GET /api/generation/status</c> (009 FR-010): never fails, so the style library stays usable without a GPU.</summary>
/// <param name="availability">Generator availability (10 s cache).</param>
public sealed class GenerationStatusService(GenerationAvailability availability)
{
    /// <summary>Reason of the voice features while no singer service is configured.</summary>
    public const string SingerNotInstalled = "The singer service is not installed.";

    /// <summary>The status.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200.</returns>
    public async Task<IResult> GetAsync(CancellationToken cancellationToken)
    {
        var readiness = await availability.GetAsync(cancellationToken);
        var models = readiness.Health?.Models ?? [];
        return TypedResults.Ok(new GenerationStatusDto
        {
            State = readiness.State,
            Reason = readiness.Reason,
            Device = readiness.Health is { } health ? Device(health.Device) : null,
            Models = models.Select(model => new GenerationModelDto { Id = model.Id, Present = model.Present, Bytes = model.Bytes }).ToList(),
            DownloadBytesNeeded = models.Where(model => !model.Present).Sum(model => model.Bytes),
            Voice = new VoiceServiceStatusDto { State = GenerationAvailabilityDto.NotInstalled, Reason = SingerNotInstalled },
        });
    }

    private static ProcessingDeviceDto Device(GeneratorDevice device)
    {
        var gpu = device.Kind == "gpu";
        return new ProcessingDeviceDto
        {
            Reachable = true,
            Kind = gpu ? ProcessingDeviceKindDto.Gpu : ProcessingDeviceKindDto.Cpu,
            Name = gpu ? device.Name : null,
            TotalMemoryMiB = gpu ? device.TotalMemoryMiB : null,
            Reason = device.Reason,
        };
    }
}
