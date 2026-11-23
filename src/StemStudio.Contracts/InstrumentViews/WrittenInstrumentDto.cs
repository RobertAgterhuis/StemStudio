using System.Text.Json.Serialization;

namespace StemStudio.Contracts.InstrumentViews;

/// <summary>Written instrument of a part (backend-api 1.15.0, feature 016, data-model 4.2).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<WrittenInstrumentDto>))]
public enum WrittenInstrumentDto
{
    /// <summary>Concert pitch (the profile's instrument).</summary>
    [JsonStringEnumMemberName("concert")]
    Concert,

    /// <summary>Trumpet in B-flat.</summary>
    [JsonStringEnumMemberName("trumpet-bb")]
    TrumpetBb,

    /// <summary>Clarinet in B-flat.</summary>
    [JsonStringEnumMemberName("clarinet-bb")]
    ClarinetBb,

    /// <summary>Soprano saxophone in B-flat.</summary>
    [JsonStringEnumMemberName("soprano-sax-bb")]
    SopranoSaxBb,

    /// <summary>Tenor saxophone in B-flat.</summary>
    [JsonStringEnumMemberName("tenor-sax-bb")]
    TenorSaxBb,

    /// <summary>Bass clarinet in B-flat.</summary>
    [JsonStringEnumMemberName("bass-clarinet-bb")]
    BassClarinetBb,

    /// <summary>Alto saxophone in E-flat.</summary>
    [JsonStringEnumMemberName("alto-sax-eb")]
    AltoSaxEb,

    /// <summary>Baritone saxophone in E-flat.</summary>
    [JsonStringEnumMemberName("baritone-sax-eb")]
    BaritoneSaxEb,

    /// <summary>Clarinet in E-flat.</summary>
    [JsonStringEnumMemberName("clarinet-eb")]
    ClarinetEb,

    /// <summary>Horn in F.</summary>
    [JsonStringEnumMemberName("horn-f")]
    HornF,

    /// <summary>English horn in F.</summary>
    [JsonStringEnumMemberName("english-horn-f")]
    EnglishHornF,

    /// <summary>Flute.</summary>
    [JsonStringEnumMemberName("flute")]
    Flute,

    /// <summary>Oboe.</summary>
    [JsonStringEnumMemberName("oboe")]
    Oboe,

    /// <summary>Violin.</summary>
    [JsonStringEnumMemberName("violin")]
    Violin,

    /// <summary>Viola.</summary>
    [JsonStringEnumMemberName("viola")]
    Viola,

    /// <summary>Cello.</summary>
    [JsonStringEnumMemberName("cello")]
    Cello,

    /// <summary>Double bass.</summary>
    [JsonStringEnumMemberName("double-bass")]
    DoubleBass,

    /// <summary>Trombone.</summary>
    [JsonStringEnumMemberName("trombone")]
    Trombone,

    /// <summary>Bassoon.</summary>
    [JsonStringEnumMemberName("bassoon")]
    Bassoon,

    /// <summary>Tuba.</summary>
    [JsonStringEnumMemberName("tuba")]
    Tuba,
}
