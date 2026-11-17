using System.Text.Json.Serialization;

namespace StemStudio.Contracts.LibraryCare;

/// <summary>Id of a storage volume (<c>Volume.id</c>) (backend-api 1.12.0, feature 013).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<StorageVolumeIdDto>))]
public enum StorageVolumeIdDto
{
    /// <summary>The API data volume.</summary>
    [JsonStringEnumMemberName("library")]
    Library,

    /// <summary>The backups folder.</summary>
    [JsonStringEnumMemberName("backups")]
    Backups,

    /// <summary>The worker data volume.</summary>
    [JsonStringEnumMemberName("workerData")]
    WorkerData,

    /// <summary>The model cache volume.</summary>
    [JsonStringEnumMemberName("modelCache")]
    ModelCache,
}
