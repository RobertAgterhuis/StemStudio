using System.Text.Json.Serialization;

namespace StemStudio.Contracts.LibraryCare;

/// <summary>Stage of a running library job (data-model 4.2 and 5.5) (backend-api 1.12.0, feature 013).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<LibraryJobStageDto>))]
public enum LibraryJobStageDto
{
    /// <summary>Backup: copying the database.</summary>
    [JsonStringEnumMemberName("snapshot")]
    Snapshot,

    /// <summary>Backup: preparing the database copy.</summary>
    [JsonStringEnumMemberName("scrub")]
    Scrub,

    /// <summary>Backup: writing the files.</summary>
    [JsonStringEnumMemberName("files")]
    Files,

    /// <summary>Backup: writing the manifest.</summary>
    [JsonStringEnumMemberName("manifest")]
    Manifest,

    /// <summary>Backup: checking the archive.</summary>
    [JsonStringEnumMemberName("check")]
    Check,

    /// <summary>Restore/verify: checking the archive.</summary>
    [JsonStringEnumMemberName("validate")]
    Validate,

    /// <summary>Restore/verify: unpacking and verifying the entries.</summary>
    [JsonStringEnumMemberName("extract")]
    Extract,

    /// <summary>Restore: checking and updating the database.</summary>
    [JsonStringEnumMemberName("database")]
    Database,

    /// <summary>Restore: switching to the restored library (not cancellable).</summary>
    [JsonStringEnumMemberName("switch")]
    Switch,

    /// <summary>Restore: finishing (not cancellable).</summary>
    [JsonStringEnumMemberName("reconcile")]
    Reconcile,
}
