using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Recording;

/// <summary>The stem a take replaces in compare mode; <c>Piano</c> is labelled "Keys", <c>Other</c> covers acoustic and other instruments (backend-api 1.14.0, feature 015).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<RecordingPartDto>))]
public enum RecordingPartDto
{
    /// <summary>No original part (compare: take on/off).</summary>
    [JsonStringEnumMemberName("none")]
    None,

    /// <summary>Vocals.</summary>
    [JsonStringEnumMemberName("vocals")]
    Vocals,

    /// <summary>Guitar.</summary>
    [JsonStringEnumMemberName("guitar")]
    Guitar,

    /// <summary>Bass.</summary>
    [JsonStringEnumMemberName("bass")]
    Bass,

    /// <summary>Keys (stem <c>piano</c>).</summary>
    [JsonStringEnumMemberName("piano")]
    Piano,

    /// <summary>Drums.</summary>
    [JsonStringEnumMemberName("drums")]
    Drums,

    /// <summary>Acoustic and other instruments.</summary>
    [JsonStringEnumMemberName("other")]
    Other,
}
