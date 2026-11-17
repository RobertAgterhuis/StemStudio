namespace StemStudio.Contracts.Practice;

/// <summary>Practice log switch and size (<c>PracticeLogSettings</c>; not 012's mix <c>PracticeSettings</c>) (backend-api 1.13.0, feature 014).</summary>
public sealed record PracticeLogSettingsDto
{
    /// <summary>Default true; while false every session upsert answers 409 <c>practice-log-off</c>.</summary>
    public required bool Enabled { get; init; }

    /// <summary>The first-use notice was confirmed.</summary>
    public required bool NoticeAcknowledged { get; init; }

    /// <summary>Number of stored sessions.</summary>
    public required int Sessions { get; init; }

    /// <summary>Last change, or null when never saved.</summary>
    public required DateTimeOffset? UpdatedAt { get; init; }
}
