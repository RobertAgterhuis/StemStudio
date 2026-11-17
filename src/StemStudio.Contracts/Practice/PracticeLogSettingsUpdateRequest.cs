using System.Text.Json.Serialization;
namespace StemStudio.Contracts.Practice;

/// <summary>Body of <c>updatePracticeLogSettings</c>; at least one member (backend-api 1.13.0, feature 014).</summary>
public sealed record PracticeLogSettingsUpdateRequest
{
    /// <summary>Switch the log on or off; null keeps it.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Enabled { get; init; }

    /// <summary>Acknowledge the notice; null keeps it.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? NoticeAcknowledged { get; init; }
}
