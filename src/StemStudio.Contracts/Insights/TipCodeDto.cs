using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Insights;

/// <summary>Tip code of data-model 5.3 (no GPU re-run tip, FR-006; the G2 tip is deferred) (backend-api 1.19.0, feature 020).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TipCodeDto>))]
public enum TipCodeDto
{
    /// <summary>Mute: the instrument is probably not in the song.</summary>
    [JsonStringEnumMemberName("not-in-song")]
    NotInSong,

    /// <summary>Use a better detail stem.</summary>
    [JsonStringEnumMemberName("use-detail-stem")]
    UseDetailStem,

    /// <summary>Split further with the detail separation.</summary>
    [JsonStringEnumMemberName("try-detail-separation")]
    TryDetailSeparation,

    /// <summary>Listen to the rest stem too.</summary>
    [JsonStringEnumMemberName("listen-to-rest")]
    ListenToRest,

    /// <summary>Hard to isolate with today's models.</summary>
    [JsonStringEnumMemberName("hard-to-isolate")]
    HardToIsolate,

    /// <summary>Clean enough to practise with.</summary>
    [JsonStringEnumMemberName("clean")]
    Clean,
}
