using System.Text.Json.Serialization;

namespace StemStudio.Contracts.ScoreFollow;

/// <summary>State of a score view (<c>ScoreViewStatus</c>) (backend-api 1.13.0, feature 014).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ScoreViewStatusDto>))]
public enum ScoreViewStatusDto
{
    /// <summary>Never requested.</summary>
    [JsonStringEnumMemberName("none")]
    None,

    /// <summary>A score-view job is queued.</summary>
    [JsonStringEnumMemberName("queued")]
    Queued,

    /// <summary>The worker lays out the pages.</summary>
    [JsonStringEnumMemberName("rendering")]
    Rendering,

    /// <summary>Pages and bar map are stored.</summary>
    [JsonStringEnumMemberName("ready")]
    Ready,

    /// <summary>The last attempt failed; <c>error</c> says why.</summary>
    [JsonStringEnumMemberName("failed")]
    Failed,
}
