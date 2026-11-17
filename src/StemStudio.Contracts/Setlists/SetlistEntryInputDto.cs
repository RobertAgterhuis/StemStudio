using System.Text.Json.Serialization;
namespace StemStudio.Contracts.Setlists;

/// <summary>One entry of <c>replaceSetlistEntries</c> (<c>SetlistEntryInput</c>) (backend-api 1.13.0, feature 014).</summary>
public sealed record SetlistEntryInputDto
{
    /// <summary>Existing entry id to keep; null for a new entry.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? Id { get; init; }

    /// <summary>Track (must exist).</summary>
    public required Guid TrackId { get; init; }

    /// <summary>Mix of that track, or null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? MixId { get; init; }

    /// <summary>Note, 0..1000 characters; default empty.</summary>
    public string Note { get; init; } = "";
}
