namespace StemStudio.Contracts.Sections;

/// <summary>The sections of a track with their detection state (<c>SectionSet</c>) (backend-api 1.13.0, feature 014).</summary>
public sealed record SectionSetDto
{
    /// <summary>Track id.</summary>
    public required Guid TrackId { get; init; }

    /// <summary>Detection state.</summary>
    public required SectionSetStatusDto Status { get; init; }

    /// <summary>Who made the list.</summary>
    public required SectionSetOriginDto Origin { get; init; }

    /// <summary>Revision; +1 on every replace (detected or edited); a replace must send the current value.</summary>
    public required int Revision { get; init; }

    /// <summary>Analyser of the last detection (<c>stems-novelty/1.0.0</c>), or null.</summary>
    public required string? Analyzer { get; init; }

    /// <summary>Snap of the last detection, or null.</summary>
    public required SectionSnapDto? Snap { get; init; }

    /// <summary>Warnings of the last detection (0..10).</summary>
    public required IReadOnlyList<SectionWarningDto> Warnings { get; init; }

    /// <summary>Reason when <c>status</c> is failed.</summary>
    public required string? Error { get; init; }

    /// <summary>Sections in time order (0..64).</summary>
    public required IReadOnlyList<SectionDto> Sections { get; init; }

    /// <summary>Last change, or null when never detected.</summary>
    public required DateTimeOffset? UpdatedAt { get; init; }
}
