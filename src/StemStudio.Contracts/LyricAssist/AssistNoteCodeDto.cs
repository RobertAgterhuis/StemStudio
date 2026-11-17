using System.Text.Json.Serialization;

namespace StemStudio.Contracts.LyricAssist;

/// <summary>Code of an <c>assist.note</c> event.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AssistNoteCodeDto>))]
public enum AssistNoteCodeDto
{
    /// <summary>A block was skipped (invalid output twice or timeout).</summary>
    [JsonStringEnumMemberName("block-skipped")]
    BlockSkipped,

    /// <summary>The CPU fallback model is used while a GPU job runs.</summary>
    [JsonStringEnumMemberName("fallback-model")]
    FallbackModel,

    /// <summary>The standing quality note (e.g. Dutch grammar with the local model).</summary>
    [JsonStringEnumMemberName("quality-note")]
    QualityNote,

    /// <summary>Generated text was withheld because it matches library lyrics.</summary>
    [JsonStringEnumMemberName("verbatim-withheld")]
    VerbatimWithheld,
}
