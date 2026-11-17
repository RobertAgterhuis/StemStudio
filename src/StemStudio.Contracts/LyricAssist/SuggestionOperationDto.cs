using System.Text.Json.Serialization;

namespace StemStudio.Contracts.LyricAssist;

/// <summary>What accepting a suggestion does (schema <c>SuggestionOperation</c>).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SuggestionOperationDto>))]
public enum SuggestionOperationDto
{
    /// <summary>Replaces the anchor text.</summary>
    [JsonStringEnumMemberName("replace")]
    Replace,

    /// <summary>Inserts lines after the anchor line.</summary>
    [JsonStringEnumMemberName("insertAfter")]
    InsertAfter,

    /// <summary>Sets the sheet title.</summary>
    [JsonStringEnumMemberName("setTitle")]
    SetTitle,

    /// <summary>Information only.</summary>
    [JsonStringEnumMemberName("none")]
    None,
}
