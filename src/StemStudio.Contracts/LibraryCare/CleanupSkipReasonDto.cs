using System.Text.Json.Serialization;

namespace StemStudio.Contracts.LibraryCare;

/// <summary>Why a clean-up item is skipped or failed (<c>CleanupSkip.reason</c>) (backend-api 1.12.0, feature 013).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CleanupSkipReasonDto>))]
public enum CleanupSkipReasonDto
{
    /// <summary>The detail phase is queued or running.</summary>
    [JsonStringEnumMemberName("detail-active")]
    DetailActive,

    /// <summary>A worker job uses the cache entry.</summary>
    [JsonStringEnumMemberName("job-active")]
    JobActive,

    /// <summary>An export reads the detail stems.</summary>
    [JsonStringEnumMemberName("export-active")]
    ExportActive,

    /// <summary>The worker did not answer.</summary>
    [JsonStringEnumMemberName("worker-unavailable")]
    WorkerUnavailable,

    /// <summary>There is nothing to remove.</summary>
    [JsonStringEnumMemberName("nothing-to-remove")]
    NothingToRemove,
}
