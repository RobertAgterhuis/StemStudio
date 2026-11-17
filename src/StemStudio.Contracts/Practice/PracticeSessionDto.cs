namespace StemStudio.Contracts.Practice;

/// <summary>A stored session (<c>PracticeSession</c>) (backend-api 1.13.0, feature 014).</summary>
public sealed record PracticeSessionDto
{
    /// <summary>Session id (made by the browser).</summary>
    public required Guid Id { get; init; }

    /// <summary>Track.</summary>
    public required Guid TrackId { get; init; }

    /// <summary>Mix, or null.</summary>
    public required Guid? MixId { get; init; }

    /// <summary>Start.</summary>
    public required DateTimeOffset StartedAt { get; init; }

    /// <summary>Last activity.</summary>
    public required DateTimeOffset EndedAt { get; init; }

    /// <summary>Playing time.</summary>
    public required double PlayedSeconds { get; init; }

    /// <summary>Loop passes.</summary>
    public required int LoopPasses { get; init; }

    /// <summary>Highest speed, or null.</summary>
    public required double? MaxSpeedPercent { get; init; }

    /// <summary>Per-section split.</summary>
    public required IReadOnlyList<PracticeSectionInputDto> Sections { get; init; }
}
