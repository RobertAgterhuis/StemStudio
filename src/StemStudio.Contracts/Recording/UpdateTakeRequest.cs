using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Recording;

/// <summary>Body of <c>updateRecordingTake</c>; at least one field (backend-api 1.14.0, feature 015).</summary>
public sealed record UpdateTakeRequest
{
    /// <summary>New name (1..80).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; init; }

    /// <summary>Star or unstar.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Favorite { get; init; }

    /// <summary>Recorded part.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RecordingPartDto? Part { get; init; }
}
