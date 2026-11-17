using System.Text.Json.Serialization;

namespace StemStudio.Contracts.LibraryCare;

/// <summary>Archive and storage category of the library (<c>Category</c>, backup-archive 1.0.0) (backend-api 1.12.0, feature 013).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<BackupCategoryDto>))]
public enum BackupCategoryDto
{
    /// <summary>The SQLite database (always included).</summary>
    [JsonStringEnumMemberName("database")]
    Database,

    /// <summary>Standard stems of the tracks (always included, original).</summary>
    [JsonStringEnumMemberName("stems")]
    Stems,

    /// <summary>Song pictures (always included).</summary>
    [JsonStringEnumMemberName("artwork")]
    Artwork,

    /// <summary>Uploads of tracks that have not completed (always included).</summary>
    [JsonStringEnumMemberName("pendingUploads")]
    PendingUploads,

    /// <summary>Detail stems (optional, regenerable by Split further).</summary>
    [JsonStringEnumMemberName("detailStems")]
    DetailStems,

    /// <summary>Audio exports and stem packs of the library (optional).</summary>
    [JsonStringEnumMemberName("exports")]
    Exports,

    /// <summary>Sheet music items (optional).</summary>
    [JsonStringEnumMemberName("scores")]
    Scores,

    /// <summary>Generated takes and converted vocals (optional).</summary>
    [JsonStringEnumMemberName("generations")]
    Generations,

    /// <summary>Play-along takes of feature 015 (optional; empty before 015).</summary>
    [JsonStringEnumMemberName("recordings")]
    Recordings,

    /// <summary>Voice profiles with their samples and consent (optional).</summary>
    [JsonStringEnumMemberName("voiceProfiles")]
    VoiceProfiles,
}
