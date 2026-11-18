using System.Text.Json.Serialization;

namespace StemStudio.Contracts.InstrumentViews;

/// <summary>Chord quality of the <c>full</c> vocabulary (backend-api 1.15.0, feature 016, data-model 2.1).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ChordQualityDto>))]
public enum ChordQualityDto
{
    /// <summary>Major.</summary>
    [JsonStringEnumMemberName("maj")]
    Maj,

    /// <summary>Minor.</summary>
    [JsonStringEnumMemberName("min")]
    Min,

    /// <summary>Diminished.</summary>
    [JsonStringEnumMemberName("dim")]
    Dim,

    /// <summary>Augmented.</summary>
    [JsonStringEnumMemberName("aug")]
    Aug,

    /// <summary>Minor sixth.</summary>
    [JsonStringEnumMemberName("min6")]
    Min6,

    /// <summary>Major sixth.</summary>
    [JsonStringEnumMemberName("maj6")]
    Maj6,

    /// <summary>Minor seventh.</summary>
    [JsonStringEnumMemberName("min7")]
    Min7,

    /// <summary>Minor major seventh.</summary>
    [JsonStringEnumMemberName("minmaj7")]
    Minmaj7,

    /// <summary>Major seventh.</summary>
    [JsonStringEnumMemberName("maj7")]
    Maj7,

    /// <summary>Dominant seventh.</summary>
    [JsonStringEnumMemberName("7")]
    Seventh,

    /// <summary>Diminished seventh.</summary>
    [JsonStringEnumMemberName("dim7")]
    Dim7,

    /// <summary>Half-diminished seventh.</summary>
    [JsonStringEnumMemberName("hdim7")]
    Hdim7,

    /// <summary>Suspended second.</summary>
    [JsonStringEnumMemberName("sus2")]
    Sus2,

    /// <summary>Suspended fourth.</summary>
    [JsonStringEnumMemberName("sus4")]
    Sus4,
}
