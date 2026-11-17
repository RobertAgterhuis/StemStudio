using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Recording;

/// <summary>How a stored input calibration was obtained (backend-api 1.14.0, feature 015).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CalibrationMethodDto>))]
public enum CalibrationMethodDto
{
    /// <summary>Measured with the loopback click test.</summary>
    [JsonStringEnumMemberName("loopback")]
    Loopback,

    /// <summary>Entered by the user.</summary>
    [JsonStringEnumMemberName("manual")]
    Manual,
}
