using System.Text.Json.Serialization;

namespace StemStudio.Contracts.LyricAssist;

/// <summary>Why a sheet revision exists (<c>LyricsRevisionSummary.cause</c>).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<RevisionCauseDto>))]
public enum RevisionCauseDto
{
    /// <summary>The sheet was created.</summary>
    [JsonStringEnumMemberName("created")]
    Created,

    /// <summary>The user saved.</summary>
    [JsonStringEnumMemberName("userSave")]
    UserSave,

    /// <summary>Suggestions were accepted.</summary>
    [JsonStringEnumMemberName("accept")]
    Accept,

    /// <summary>An undo or restore.</summary>
    [JsonStringEnumMemberName("undo")]
    Undo,
}
