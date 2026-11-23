using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Insights;

/// <summary>Kind of separation artefact (backend-api 1.19.0, feature 020).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ArtefactKindDto>))]
public enum ArtefactKindDto
{
    /// <summary>Time-frequency flicker (watery sound).</summary>
    [JsonStringEnumMemberName("flicker")]
    Flicker,

    /// <summary>Gating holes.</summary>
    [JsonStringEnumMemberName("dropouts")]
    Dropouts,

    /// <summary>The detail model left a large residual (002).</summary>
    [JsonStringEnumMemberName("model-residual")]
    ModelResidual,

    /// <summary>Much of the parent went to the rest stem.</summary>
    [JsonStringEnumMemberName("rest-share")]
    RestShare,
}
