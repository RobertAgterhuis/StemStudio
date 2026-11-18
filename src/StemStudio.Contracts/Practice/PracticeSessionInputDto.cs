using System.Text.Json.Serialization;
namespace StemStudio.Contracts.Practice;

/// <summary>Body of <c>upsertPracticeSession</c>: cumulative values (<c>PracticeSessionInput</c>) (backend-api 1.13.0, feature 014).</summary>
public sealed record PracticeSessionInputDto
{
    /// <summary>Track.</summary>
    public required Guid TrackId { get; init; }

    /// <summary>Mix loaded during the session, or null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? MixId { get; init; }

    /// <summary>Session start (UTC).</summary>
    public required DateTimeOffset StartedAt { get; init; }

    /// <summary>Last activity (UTC); at most 12 h after the start.</summary>
    public required DateTimeOffset EndedAt { get; init; }

    /// <summary>Wall-clock playing time.</summary>
    public required double PlayedSeconds { get; init; }

    /// <summary>Completed loop passes.</summary>
    public int LoopPasses { get; init; }

    /// <summary>Highest playback speed, or null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? MaxSpeedPercent { get; init; }

    /// <summary>Per-section split (0..64).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<PracticeSectionInputDto>? Sections { get; init; }
}
