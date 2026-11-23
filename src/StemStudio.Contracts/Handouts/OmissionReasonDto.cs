using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Handouts;

/// <summary>Why an ingredient was left out (backend-api 1.19.0, feature 020).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<OmissionReasonDto>))]
public enum OmissionReasonDto
{
    /// <summary>It does not exist yet.</summary>
    [JsonStringEnumMemberName("not-available")]
    NotAvailable,

    /// <summary>The user left it out.</summary>
    [JsonStringEnumMemberName("not-selected")]
    NotSelected,

    /// <summary>No timings to place it.</summary>
    [JsonStringEnumMemberName("no-timing")]
    NoTiming,
}
