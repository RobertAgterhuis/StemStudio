using System.Text.Json.Serialization;

namespace StemStudio.Contracts.LibraryCare;

/// <summary>Why a restore skipped an item (<c>SkippedItem.reason</c>) (backend-api 1.12.0, feature 013).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SkippedItemReasonDto>))]
public enum SkippedItemReasonDto
{
    /// <summary>Merge: an item with the same id exists.</summary>
    [JsonStringEnumMemberName("same-id")]
    SameId,

    /// <summary>Merge: a track of the same source exists.</summary>
    [JsonStringEnumMemberName("same-source")]
    SameSource,

    /// <summary>Merge: an item it depends on was skipped.</summary>
    [JsonStringEnumMemberName("dependency-skipped")]
    DependencySkipped,

    /// <summary>A voice profile without a valid consent proof.</summary>
    [JsonStringEnumMemberName("consent-invalid")]
    ConsentInvalid,

    /// <summary>Left out by the restore options.</summary>
    [JsonStringEnumMemberName("option")]
    Option,

    /// <summary>A file of the item was missing in the archive.</summary>
    [JsonStringEnumMemberName("missing-file")]
    MissingFile,
}
