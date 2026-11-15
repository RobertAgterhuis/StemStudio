using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Assets;

/// <summary>Status of a library item; <c>ready</c> and <c>failed</c> are terminal.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AssetStatusDto>))]
public enum AssetStatusDto
{
    /// <summary>Waiting for the worker.</summary>
    [JsonStringEnumMemberName("queued")]
    Queued,

    /// <summary>Being rendered; see the progress.</summary>
    [JsonStringEnumMemberName("rendering")]
    Rendering,

    /// <summary>Files are available.</summary>
    [JsonStringEnumMemberName("ready")]
    Ready,

    /// <summary>Rendering failed; see the error. Retryable.</summary>
    [JsonStringEnumMemberName("failed")]
    Failed,
}
