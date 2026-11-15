using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Tracks;

/// <summary>What the detail phase did with one standard stem (FR-003..FR-006). Serialized as kebab-case strings.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ParentOutcomeKindDto>))]
public enum ParentOutcomeKindDto
{
    /// <summary>Split into at least two detail stems plus <c>rest</c> ("Split into ...").</summary>
    [JsonStringEnumMemberName("split")]
    Split,

    /// <summary>Not split because the stem is (almost) silent.</summary>
    [JsonStringEnumMemberName("skipped-silent")]
    SkippedSilent,

    /// <summary>Fewer than two detail stems were audible ("Nothing further to separate").</summary>
    [JsonStringEnumMemberName("nothing-further")]
    NothingFurther,

    /// <summary>The split failed the plausibility or reconstruction check ("Split was not reliable").</summary>
    [JsonStringEnumMemberName("not-reliable")]
    NotReliable,

    /// <summary>No detail model exists for this stem (bass) ("Not split").</summary>
    [JsonStringEnumMemberName("not-supported")]
    NotSupported,
}
