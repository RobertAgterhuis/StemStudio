using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Recording;

/// <summary>Storage state of a take (backend-api 1.14.0, feature 015).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TakeStatusDto>))]
public enum TakeStatusDto
{
    /// <summary>Uploaded; the FLAC is being written.</summary>
    [JsonStringEnumMemberName("saving")]
    Saving,

    /// <summary>Stored and playable.</summary>
    [JsonStringEnumMemberName("ready")]
    Ready,

    /// <summary>Saving failed; <c>retryRecordingTake</c> may help.</summary>
    [JsonStringEnumMemberName("failed")]
    Failed,
}
