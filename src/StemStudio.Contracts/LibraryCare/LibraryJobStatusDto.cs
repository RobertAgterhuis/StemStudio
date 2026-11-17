using System.Text.Json.Serialization;

namespace StemStudio.Contracts.LibraryCare;

/// <summary>Status of a library job; <c>completed</c>, <c>failed</c> and <c>cancelled</c> are terminal (backend-api 1.12.0, feature 013).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<LibraryJobStatusDto>))]
public enum LibraryJobStatusDto
{
    /// <summary>Accepted, not started.</summary>
    [JsonStringEnumMemberName("queued")]
    Queued,

    /// <summary>Running; see the stage and progress.</summary>
    [JsonStringEnumMemberName("running")]
    Running,

    /// <summary>Finished; see the report.</summary>
    [JsonStringEnumMemberName("completed")]
    Completed,

    /// <summary>Failed; see the error.</summary>
    [JsonStringEnumMemberName("failed")]
    Failed,

    /// <summary>Cancelled by the user.</summary>
    [JsonStringEnumMemberName("cancelled")]
    Cancelled,
}
