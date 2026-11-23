using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Insights;

/// <summary>Rating of a stem (backend-api 1.19.0, feature 020).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<QualityRatingDto>))]
public enum QualityRatingDto
{
    /// <summary>Good.</summary>
    [JsonStringEnumMemberName("good")]
    Good,

    /// <summary>Fair.</summary>
    [JsonStringEnumMemberName("fair")]
    Fair,

    /// <summary>Poor.</summary>
    [JsonStringEnumMemberName("poor")]
    Poor,

    /// <summary>Probably not in this song.</summary>
    [JsonStringEnumMemberName("not-present")]
    NotPresent,

    /// <summary>Not rated (too little audio).</summary>
    [JsonStringEnumMemberName("unknown")]
    Unknown,
}
