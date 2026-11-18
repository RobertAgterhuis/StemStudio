using System.Text.Json.Serialization;

namespace StemStudio.Contracts.InstrumentViews;

/// <summary>Status of a per-track analysis (backend-api 1.15.0, feature 016, data-model 7.2).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AnalysisStatusDto>))]
public enum AnalysisStatusDto
{
    /// <summary>Never run (or deleted).</summary>
    [JsonStringEnumMemberName("none")]
    None,

    /// <summary>Waiting on the worker lane.</summary>
    [JsonStringEnumMemberName("queued")]
    Queued,

    /// <summary>Running.</summary>
    [JsonStringEnumMemberName("running")]
    Running,

    /// <summary>The document is ready.</summary>
    [JsonStringEnumMemberName("ready")]
    Ready,

    /// <summary>Failed; <c>reason</c> says why.</summary>
    [JsonStringEnumMemberName("failed")]
    Failed,

    /// <summary>A prerequisite is missing (drums not split, no vocals stem, no harmonic stem).</summary>
    [JsonStringEnumMemberName("unavailable")]
    Unavailable,

    /// <summary>The input stems changed; the old document is still shown.</summary>
    [JsonStringEnumMemberName("stale")]
    Stale,
}
