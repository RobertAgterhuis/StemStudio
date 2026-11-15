using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Tracks;

/// <summary>Device a job ran on (backend contract 1.3.0, feature 003). Serialized as lower-case strings.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ProcessingDeviceKindDto>))]
public enum ProcessingDeviceKindDto
{
    /// <summary>The CPU.</summary>
    [JsonStringEnumMemberName("cpu")]
    Cpu,

    /// <summary>The NVIDIA GPU (CUDA).</summary>
    [JsonStringEnumMemberName("gpu")]
    Gpu,
}
