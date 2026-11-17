using System.Text.Json.Serialization;

namespace StemStudio.Contracts.LyricAssist;

/// <summary>Layer that produced a suggestion (schema <c>SuggestionSource</c>; also <c>LayerInfo.layer</c>).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SuggestionSourceDto>))]
public enum SuggestionSourceDto
{
    /// <summary>Hunspell dictionaries and the personal word list.</summary>
    [JsonStringEnumMemberName("dictionary")]
    Dictionary,

    /// <summary>Deterministic rules (licence lexicon, syllables, rhymes).</summary>
    [JsonStringEnumMemberName("rules")]
    Rules,

    /// <summary>The assistant model.</summary>
    [JsonStringEnumMemberName("model")]
    Model,
}
