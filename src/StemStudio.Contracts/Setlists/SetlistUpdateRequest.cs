using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Setlists;

/// <summary>Body of <c>updateSetlist</c> (<c>SetlistUpdate</c>); at least one member (backend-api 1.13.0, feature 014).</summary>
public sealed record SetlistUpdateRequest
{
    /// <summary>New name, or null to keep.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; init; }

    /// <summary>New note, or null to keep.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Note { get; init; }
}
