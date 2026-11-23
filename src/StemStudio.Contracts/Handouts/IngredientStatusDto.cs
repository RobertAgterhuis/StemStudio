using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Handouts;

/// <summary>Whether an ingredient exists for the source (backend-api 1.19.0, feature 020).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<IngredientStatusDto>))]
public enum IngredientStatusDto
{
    /// <summary>Exists.</summary>
    [JsonStringEnumMemberName("available")]
    Available,

    /// <summary>Could exist but does not yet.</summary>
    [JsonStringEnumMemberName("missing")]
    Missing,

    /// <summary>Not offered for this family or source.</summary>
    [JsonStringEnumMemberName("not-applicable")]
    NotApplicable,
}
