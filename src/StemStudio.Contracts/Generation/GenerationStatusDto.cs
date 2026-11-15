using StemStudio.Contracts.Health;

namespace StemStudio.Contracts.Generation;

/// <summary>Body of <c>GET /api/generation/status</c> (1.8.0; <see cref="Voice"/> 1.9.0).</summary>
public sealed record GenerationStatusDto
{
    /// <summary>Availability of the generator.</summary>
    public required GenerationAvailabilityDto State { get; init; }

    /// <summary>Readable reason when not ready.</summary>
    public string? Reason { get; init; }

    /// <summary>The generator's device; null when not installed.</summary>
    public ProcessingDeviceDto? Device { get; init; }

    /// <summary>Model files the generator and the singer need.</summary>
    public IReadOnlyList<GenerationModelDto> Models { get; init; } = [];

    /// <summary>Bytes of the models that are not present yet.</summary>
    public long DownloadBytesNeeded { get; init; }

    /// <summary>Availability of the singer service (1.9.0).</summary>
    public VoiceServiceStatusDto Voice { get; init; } = new() { State = GenerationAvailabilityDto.NotInstalled };
}
