using System.Text.Json.Serialization;

namespace StemStudio.Contracts.LibraryCare;

/// <summary>Category of a storage overview row (<c>StorageCategory</c>) (backend-api 1.12.0, feature 013).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<StorageCategoryDto>))]
public enum StorageCategoryDto
{
    /// <summary>The SQLite database including <c>-wal</c> and <c>-shm</c>.</summary>
    [JsonStringEnumMemberName("database")]
    Database,

    /// <summary>Standard stems.</summary>
    [JsonStringEnumMemberName("stems")]
    Stems,

    /// <summary>Song pictures.</summary>
    [JsonStringEnumMemberName("artwork")]
    Artwork,

    /// <summary>Uploads of tracks that have not completed.</summary>
    [JsonStringEnumMemberName("pendingUploads")]
    PendingUploads,

    /// <summary>Detail stems.</summary>
    [JsonStringEnumMemberName("detailStems")]
    DetailStems,

    /// <summary>Audio exports and stem packs.</summary>
    [JsonStringEnumMemberName("exports")]
    Exports,

    /// <summary>Sheet music items.</summary>
    [JsonStringEnumMemberName("scores")]
    Scores,

    /// <summary>Generated takes and converted vocals.</summary>
    [JsonStringEnumMemberName("generations")]
    Generations,

    /// <summary>Play-along takes (feature 015).</summary>
    [JsonStringEnumMemberName("recordings")]
    Recordings,

    /// <summary>Voice samples.</summary>
    [JsonStringEnumMemberName("voiceProfiles")]
    VoiceProfiles,

    /// <summary>Temporary files, orphan folders and unreferenced uploads.</summary>
    [JsonStringEnumMemberName("leftovers")]
    Leftovers,

    /// <summary>Staging folder of a running or interrupted library job.</summary>
    [JsonStringEnumMemberName("restoreStaging")]
    RestoreStaging,

    /// <summary>The library moved aside by a replace restore.</summary>
    [JsonStringEnumMemberName("previousLibrary")]
    PreviousLibrary,

    /// <summary>Worker cache of standard separations.</summary>
    [JsonStringEnumMemberName("workerSeparationCache")]
    WorkerSeparationCache,

    /// <summary>Worker cache of detail separations.</summary>
    [JsonStringEnumMemberName("workerDetailCache")]
    WorkerDetailCache,

    /// <summary>Worker job records.</summary>
    [JsonStringEnumMemberName("workerJobFiles")]
    WorkerJobFiles,

    /// <summary>Worker scratch folders and uploads.</summary>
    [JsonStringEnumMemberName("workerScratch")]
    WorkerScratch,
}
