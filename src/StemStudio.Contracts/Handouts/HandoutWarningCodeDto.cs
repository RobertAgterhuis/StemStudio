using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Handouts;

/// <summary>Warning of the handout renderer (worker <c>HandoutWarningCode</c>) (backend-api 1.19.0, feature 020).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<HandoutWarningCodeDto>))]
public enum HandoutWarningCodeDto
{
    /// <summary>Characters no installed font has.</summary>
    [JsonStringEnumMemberName("missing-glyphs")]
    MissingGlyphs,

    /// <summary>Notation pages were scaled to the paper.</summary>
    [JsonStringEnumMemberName("notation-scaled")]
    NotationScaled,

    /// <summary>A notation file could not be read; its pages were replaced by a note.</summary>
    [JsonStringEnumMemberName("notation-unreadable")]
    NotationUnreadable,
}
