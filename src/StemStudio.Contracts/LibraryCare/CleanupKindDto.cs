using System.Text.Json.Serialization;

namespace StemStudio.Contracts.LibraryCare;

/// <summary>Kind of a clean-up action (<c>CleanupKind</c>); only regenerable data (backend-api 1.12.0, feature 013).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CleanupKindDto>))]
public enum CleanupKindDto
{
    /// <summary>Detail stems (Split further restores them).</summary>
    [JsonStringEnumMemberName("detailStems")]
    DetailStems,

    /// <summary>Worker caches (rebuilt from the library stems).</summary>
    [JsonStringEnumMemberName("workerCache")]
    WorkerCache,

    /// <summary>Temporary files, orphans, finished uploads, worker scratch and old job files.</summary>
    [JsonStringEnumMemberName("leftovers")]
    Leftovers,
}
