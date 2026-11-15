using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Lyrics;

/// <summary>Lyrics state of a track (backend contract 1.6.0, data-model.md 3.2 of feature 006).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<LyricsStatusDto>))]
public enum LyricsStatusDto
{
    /// <summary>Never looked up (older tracks, or before the duration is known).</summary>
    [JsonStringEnumMemberName("none")]
    None,

    /// <summary>The LRCLIB lookup is running.</summary>
    [JsonStringEnumMemberName("searching")]
    Searching,

    /// <summary>Timed lyrics are available (the banner is shown).</summary>
    [JsonStringEnumMemberName("found")]
    Found,

    /// <summary>LRCLIB marks the song as instrumental (no banner).</summary>
    [JsonStringEnumMemberName("instrumental")]
    Instrumental,

    /// <summary>No lyrics were found online; the user can enter them.</summary>
    [JsonStringEnumMemberName("notFound")]
    NotFound,

    /// <summary>Text without timing; the alignment has not started (CPU worker).</summary>
    [JsonStringEnumMemberName("alignPending")]
    AlignPending,

    /// <summary>The text is being aligned to the vocal stem.</summary>
    [JsonStringEnumMemberName("aligning")]
    Aligning,

    /// <summary>Lookup or alignment failed; see the reason.</summary>
    [JsonStringEnumMemberName("failed")]
    Failed,

    /// <summary>Online lookups are switched off.</summary>
    [JsonStringEnumMemberName("disabled")]
    Disabled,

    /// <summary>Removed by the user; no automatic lookup any more.</summary>
    [JsonStringEnumMemberName("removed")]
    Removed,
}
