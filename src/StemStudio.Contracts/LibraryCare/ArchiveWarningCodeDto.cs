using System.Text.Json.Serialization;

namespace StemStudio.Contracts.LibraryCare;

/// <summary>Code of an archive warning (<c>ArchiveWarning.code</c>) (backend-api 1.12.0, feature 013).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ArchiveWarningCodeDto>))]
public enum ArchiveWarningCodeDto
{
    /// <summary>A referenced file was missing and is not in the archive.</summary>
    [JsonStringEnumMemberName("missing-file")]
    MissingFile,

    /// <summary>An item changed after the database snapshot and is not included.</summary>
    [JsonStringEnumMemberName("changed-after-snapshot")]
    ChangedAfterSnapshot,

    /// <summary>A voice profile without a complete consent record was left out.</summary>
    [JsonStringEnumMemberName("voice-profile-without-consent")]
    VoiceProfileWithoutConsent,

    /// <summary>An item was not finished at snapshot time.</summary>
    [JsonStringEnumMemberName("interrupted-entity")]
    InterruptedEntity,
}
