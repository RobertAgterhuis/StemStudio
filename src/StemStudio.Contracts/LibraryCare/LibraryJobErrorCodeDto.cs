using System.Text.Json.Serialization;

namespace StemStudio.Contracts.LibraryCare;

/// <summary>Code of a library job error (<c>JobError.code</c>) (backend-api 1.12.0, feature 013).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<LibraryJobErrorCodeDto>))]
public enum LibraryJobErrorCodeDto
{
    /// <summary>The API stopped while the job ran.</summary>
    [JsonStringEnumMemberName("interrupted")]
    Interrupted,

    /// <summary>Not enough free space.</summary>
    [JsonStringEnumMemberName("insufficient-disk-space")]
    InsufficientDiskSpace,

    /// <summary>The backups folder is missing or not writable.</summary>
    [JsonStringEnumMemberName("backups-folder-unavailable")]
    BackupsFolderUnavailable,

    /// <summary>Not a readable StemStudio archive.</summary>
    [JsonStringEnumMemberName("archive-invalid")]
    ArchiveInvalid,

    /// <summary>The archive needs a newer StemStudio.</summary>
    [JsonStringEnumMemberName("archive-incompatible")]
    ArchiveIncompatible,

    /// <summary>An entry violates the layout or safety rules; nothing was changed.</summary>
    [JsonStringEnumMemberName("archive-unsafe")]
    ArchiveUnsafe,

    /// <summary>An entry does not match its size or SHA-256.</summary>
    [JsonStringEnumMemberName("archive-corrupt")]
    ArchiveCorrupt,

    /// <summary>The older database could not be updated.</summary>
    [JsonStringEnumMemberName("migration-failed")]
    MigrationFailed,

    /// <summary>A file operation failed (also: busy database, failed switch).</summary>
    [JsonStringEnumMemberName("io-error")]
    IoError,
}
