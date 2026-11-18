using System.Text.Json.Serialization;
using StemStudio.Contracts.Scores;

namespace StemStudio.Contracts.ScoreFollow;

/// <summary>A ready score of a track (<c>TrackScore</c>) (backend-api 1.13.0, feature 014).</summary>
public sealed record TrackScoreDto
{
    /// <summary>Score asset id.</summary>
    public required Guid AssetId { get; init; }

    /// <summary>Asset name.</summary>
    public required string Name { get; init; }

    /// <summary>Instrument profile (008).</summary>
    public required ScoreProfileDto Profile { get; init; }

    /// <summary>What it was made from (channel id or <c>audio-asset</c>).</summary>
    public required string Source { get; init; }

    /// <summary>Track time of the score's input start, when made from a range.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? RangeStartSeconds { get; init; }

    /// <summary>Track time of the score's input end, when made from a range.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? RangeEndSeconds { get; init; }

    /// <summary>True when the score was made with a manual tempo.</summary>
    public bool ManualTempo { get; init; }

    /// <summary>When the score was made.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>State of its score view.</summary>
    public required ScoreViewStatusDto ViewStatus { get; init; }
}
