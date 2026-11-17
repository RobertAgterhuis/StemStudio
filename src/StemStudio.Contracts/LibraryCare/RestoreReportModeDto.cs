using System.Text.Json.Serialization;

namespace StemStudio.Contracts.LibraryCare;

/// <summary>What a finished restore did (<c>RestoreReport.mode</c>) (backend-api 1.12.0, feature 013).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<RestoreReportModeDto>))]
public enum RestoreReportModeDto
{
    /// <summary>Restored into an empty library.</summary>
    [JsonStringEnumMemberName("empty")]
    Empty,

    /// <summary>Replaced the library; the previous one was kept aside.</summary>
    [JsonStringEnumMemberName("replace")]
    Replace,

    /// <summary>Merged into the library.</summary>
    [JsonStringEnumMemberName("merge")]
    Merge,

    /// <summary>Switched back to the previous library.</summary>
    [JsonStringEnumMemberName("undo")]
    Undo,
}
