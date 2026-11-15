using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Tracks;

/// <summary>Status of the detail phase of a track (feature 002, FR-007). Serialized as lower-case strings.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<DetailPhaseStatusDto>))]
public enum DetailPhaseStatusDto
{
    /// <summary>Never requested (imported before feature 002, or standard separation not completed yet).</summary>
    [JsonStringEnumMemberName("none")]
    None,

    /// <summary>Waiting for the worker (also while the submission is pending).</summary>
    [JsonStringEnumMemberName("queued")]
    Queued,

    /// <summary>A parent stem is being split.</summary>
    [JsonStringEnumMemberName("running")]
    Running,

    /// <summary>Per-parent outcomes and detail stems are available.</summary>
    [JsonStringEnumMemberName("completed")]
    Completed,

    /// <summary>The detail phase failed; see the error text. Retryable.</summary>
    [JsonStringEnumMemberName("failed")]
    Failed,

    /// <summary>Cancelled by the user. Retryable.</summary>
    [JsonStringEnumMemberName("cancelled")]
    Cancelled,
}
