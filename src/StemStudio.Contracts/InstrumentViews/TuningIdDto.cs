using System.Text.Json.Serialization;

namespace StemStudio.Contracts.InstrumentViews;

/// <summary>Tuning catalogue id (backend-api 1.15.0, feature 016, data-model 4.1).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TuningIdDto>))]
public enum TuningIdDto
{
    /// <summary>Guitar standard (E A D G B E).</summary>
    [JsonStringEnumMemberName("guitar-standard")]
    GuitarStandard,

    /// <summary>Guitar drop D.</summary>
    [JsonStringEnumMemberName("guitar-drop-d")]
    GuitarDropD,

    /// <summary>Guitar half step down.</summary>
    [JsonStringEnumMemberName("guitar-half-step-down")]
    GuitarHalfStepDown,

    /// <summary>Guitar full step down.</summary>
    [JsonStringEnumMemberName("guitar-full-step-down")]
    GuitarFullStepDown,

    /// <summary>Guitar DADGAD.</summary>
    [JsonStringEnumMemberName("guitar-dadgad")]
    GuitarDadgad,

    /// <summary>Guitar open G.</summary>
    [JsonStringEnumMemberName("guitar-open-g")]
    GuitarOpenG,

    /// <summary>Guitar open D.</summary>
    [JsonStringEnumMemberName("guitar-open-d")]
    GuitarOpenD,

    /// <summary>4-string bass (E A D G).</summary>
    [JsonStringEnumMemberName("bass-4-standard")]
    Bass4Standard,

    /// <summary>4-string bass drop D.</summary>
    [JsonStringEnumMemberName("bass-4-drop-d")]
    Bass4DropD,

    /// <summary>4-string bass half step down.</summary>
    [JsonStringEnumMemberName("bass-4-half-step-down")]
    Bass4HalfStepDown,

    /// <summary>5-string bass (B E A D G).</summary>
    [JsonStringEnumMemberName("bass-5-standard")]
    Bass5Standard,

    /// <summary>6-string bass (B E A D G C).</summary>
    [JsonStringEnumMemberName("bass-6-standard")]
    Bass6Standard,
}
