using System.Text.Json.Serialization;

namespace StemStudio.Contracts.LibraryCare;

/// <summary>Restore mode of a request (<c>RestoreOptions.mode</c>) (backend-api 1.12.0, feature 013).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<RestoreModeDto>))]
public enum RestoreModeDto
{
    /// <summary>Replace the library (also covers an empty library; the old one is kept aside).</summary>
    [JsonStringEnumMemberName("replace")]
    Replace,

    /// <summary>Add the missing items to the library (P3).</summary>
    [JsonStringEnumMemberName("merge")]
    Merge,
}
