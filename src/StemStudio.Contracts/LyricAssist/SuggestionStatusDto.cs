using System.Text.Json.Serialization;

namespace StemStudio.Contracts.LyricAssist;

/// <summary>Lifecycle state of a suggestion (schema <c>SuggestionStatus</c>, data-model section 9).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SuggestionStatusDto>))]
public enum SuggestionStatusDto
{
    /// <summary>Not decided yet.</summary>
    [JsonStringEnumMemberName("open")]
    Open,

    /// <summary>Applied to the text.</summary>
    [JsonStringEnumMemberName("accepted")]
    Accepted,

    /// <summary>Rejected by the user.</summary>
    [JsonStringEnumMemberName("rejected")]
    Rejected,

    /// <summary>Marked as intentional.</summary>
    [JsonStringEnumMemberName("intentional")]
    Intentional,

    /// <summary>Its anchor text is gone.</summary>
    [JsonStringEnumMemberName("stale")]
    Stale,

    /// <summary>Another alternative of its group was accepted.</summary>
    [JsonStringEnumMemberName("superseded")]
    Superseded,
}
