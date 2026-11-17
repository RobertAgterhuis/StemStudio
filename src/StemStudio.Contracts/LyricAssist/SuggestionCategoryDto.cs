using System.Text.Json.Serialization;

namespace StemStudio.Contracts.LyricAssist;

/// <summary>Category of a suggestion (schema <c>SuggestionCategory</c>).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SuggestionCategoryDto>))]
public enum SuggestionCategoryDto
{
    /// <summary>Spelling.</summary>
    [JsonStringEnumMemberName("spelling")]
    Spelling,

    /// <summary>Grammar.</summary>
    [JsonStringEnumMemberName("grammar")]
    Grammar,

    /// <summary>Flow and clarity.</summary>
    [JsonStringEnumMemberName("flow")]
    Flow,

    /// <summary>Poetic licence.</summary>
    [JsonStringEnumMemberName("licence")]
    Licence,

    /// <summary>Stylistic device.</summary>
    [JsonStringEnumMemberName("device")]
    Device,

    /// <summary>Alternative line.</summary>
    [JsonStringEnumMemberName("alternative")]
    Alternative,

    /// <summary>Continuation lines.</summary>
    [JsonStringEnumMemberName("continuation")]
    Continuation,

    /// <summary>Title idea.</summary>
    [JsonStringEnumMemberName("title")]
    Title,
}
