using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Tracks;

/// <summary>State of the online lookup of a track (backend contract 1.4.0, data-model.md 3.4 of feature 005).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<MetadataStatusDto>))]
public enum MetadataStatusDto
{
    /// <summary>Never looked up (tracks from before 1.4.0 until first opened).</summary>
    [JsonStringEnumMemberName("none")]
    None,

    /// <summary>Scheduled; the metadata job is about to be created.</summary>
    [JsonStringEnumMemberName("pending")]
    Pending,

    /// <summary>The worker's metadata job is running.</summary>
    [JsonStringEnumMemberName("searching")]
    Searching,

    /// <summary>Recording and artist facts are available.</summary>
    [JsonStringEnumMemberName("found")]
    Found,

    /// <summary>No reliable match; nothing is guessed.</summary>
    [JsonStringEnumMemberName("notFound")]
    NotFound,

    /// <summary>The services could not be reached; see the reason. Retryable.</summary>
    [JsonStringEnumMemberName("failed")]
    Failed,

    /// <summary>Online lookups are switched off.</summary>
    [JsonStringEnumMemberName("disabled")]
    Disabled,
}
