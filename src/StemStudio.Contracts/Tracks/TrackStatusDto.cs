using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Tracks;

/// <summary>Processing status of a track (spec FR-008). Serialized as lower-case strings.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TrackStatusDto>))]
public enum TrackStatusDto
{
    /// <summary>Waiting for the worker.</summary>
    [JsonStringEnumMemberName("queued")]
    Queued,

    /// <summary>Fetching or validating the source audio.</summary>
    [JsonStringEnumMemberName("downloading")]
    Downloading,

    /// <summary>Running stem separation.</summary>
    [JsonStringEnumMemberName("separating")]
    Separating,

    /// <summary>Detecting tempo and beat grid.</summary>
    [JsonStringEnumMemberName("analysing")]
    Analysing,

    /// <summary>Stems and analysis are available.</summary>
    [JsonStringEnumMemberName("completed")]
    Completed,

    /// <summary>Processing failed; see the error text. Retryable.</summary>
    [JsonStringEnumMemberName("failed")]
    Failed,
}
