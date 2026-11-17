using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Library;

/// <summary>Counts of a folder (backend-api 1.22.0 <c>FolderCounts</c>), counted with the <c>kind</c>/<c>status</c> filters.</summary>
public sealed record FolderCountsDto
{
    /// <summary>Albums (artist folders only; absent on the wire otherwise).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Albums { get; init; }

    /// <summary>Songs (artist and album folders; absent on the wire otherwise).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Songs { get; init; }

    /// <summary>Items inside (0 only for the root of an empty library).</summary>
    public required int Items { get; init; }
}
