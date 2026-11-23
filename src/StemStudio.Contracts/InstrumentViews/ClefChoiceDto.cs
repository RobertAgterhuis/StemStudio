using System.Text.Json.Serialization;

namespace StemStudio.Contracts.InstrumentViews;

/// <summary>Clef of a part (backend-api 1.15.0, feature 016, data-model 4.3).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ClefChoiceDto>))]
public enum ClefChoiceDto
{
    /// <summary>The instrument's default clef.</summary>
    [JsonStringEnumMemberName("auto")]
    Auto,

    /// <summary>Treble clef.</summary>
    [JsonStringEnumMemberName("treble")]
    Treble,

    /// <summary>Bass clef.</summary>
    [JsonStringEnumMemberName("bass")]
    Bass,

    /// <summary>Alto clef.</summary>
    [JsonStringEnumMemberName("alto")]
    Alto,

    /// <summary>Tenor clef.</summary>
    [JsonStringEnumMemberName("tenor")]
    Tenor,

    /// <summary>Treble clef an octave lower.</summary>
    [JsonStringEnumMemberName("treble-8vb")]
    Treble8vb,

    /// <summary>Bass clef an octave lower.</summary>
    [JsonStringEnumMemberName("bass-8vb")]
    Bass8vb,
}
