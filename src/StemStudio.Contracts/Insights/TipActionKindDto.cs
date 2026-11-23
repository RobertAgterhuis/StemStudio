using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Insights;

/// <summary>What a tip's button starts (the G2 re-separation action is deferred) (backend-api 1.19.0, feature 020).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TipActionKindDto>))]
public enum TipActionKindDto
{
    /// <summary>Mute the stem in the current mix.</summary>
    [JsonStringEnumMemberName("mute")]
    Mute,

    /// <summary>Open the mixer with the detail stem expanded.</summary>
    [JsonStringEnumMemberName("open-detail")]
    OpenDetail,

    /// <summary>Start the 002 detail separation.</summary>
    [JsonStringEnumMemberName("split-detail")]
    SplitDetail,
}
