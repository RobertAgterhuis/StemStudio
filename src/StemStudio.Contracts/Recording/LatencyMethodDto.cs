using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Recording;

/// <summary>How the latency of a take was obtained (backend-api 1.14.0, feature 015).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<LatencyMethodDto>))]
public enum LatencyMethodDto
{
    /// <summary>Measured with the loopback click test.</summary>
    [JsonStringEnumMemberName("loopback")]
    Loopback,

    /// <summary>Entered by the user.</summary>
    [JsonStringEnumMemberName("manual")]
    Manual,

    /// <summary>Browser estimate (not calibrated).</summary>
    [JsonStringEnumMemberName("estimated")]
    Estimated,
}
