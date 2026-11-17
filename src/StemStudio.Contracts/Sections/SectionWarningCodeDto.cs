using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Sections;

/// <summary>Warning codes of a section detection (backend-api 1.13.0, feature 014).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SectionWarningCodeDto>))]
public enum SectionWarningCodeDto
{
    /// <summary>The beat grid is unreliable; boundaries may be off.</summary>
    [JsonStringEnumMemberName("grid-unreliable")]
    GridUnreliable,

    /// <summary>No beat grid; boundaries in seconds.</summary>
    [JsonStringEnumMemberName("no-grid")]
    NoGrid,

    /// <summary>Nothing repeats; parts are named Part A, Part B.</summary>
    [JsonStringEnumMemberName("few-repetitions")]
    FewRepetitions,

    /// <summary>No vocals found.</summary>
    [JsonStringEnumMemberName("no-vocals")]
    NoVocals,

    /// <summary>Short sections were merged to stay within 64.</summary>
    [JsonStringEnumMemberName("merged-to-limit")]
    MergedToLimit,
}
