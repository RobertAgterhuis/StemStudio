using System.Text.Json.Serialization;

namespace StemStudio.Contracts.LibraryCare;

/// <summary>Mode of the library write gate (<c>GateMode</c>, data-model 5.2) (backend-api 1.12.0, feature 013).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<GateModeDto>))]
public enum GateModeDto
{
    /// <summary>Everything is allowed.</summary>
    [JsonStringEnumMemberName("open")]
    Open,

    /// <summary>A backup runs: operations that delete or replace library files answer 409 backup-running.</summary>
    [JsonStringEnumMemberName("backupRunning")]
    BackupRunning,

    /// <summary>A restore switches the library: every API operation except the status answers 503.</summary>
    [JsonStringEnumMemberName("maintenance")]
    Maintenance,
}
