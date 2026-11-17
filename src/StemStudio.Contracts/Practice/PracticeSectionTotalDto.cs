namespace StemStudio.Contracts.Practice;

/// <summary>Totals of one section over the window (<c>PracticeSectionTotal</c>) (backend-api 1.13.0, feature 014).</summary>
public sealed record PracticeSectionTotalDto
{
    /// <summary>Section id, or null (grouped by label and kind).</summary>
    public required Guid? SectionId { get; init; }

    /// <summary>Kind (a <c>SectionKind</c> or <c>none</c>).</summary>
    public required string Kind { get; init; }

    /// <summary>Current label when the section still exists, else the snapshot.</summary>
    public required string Label { get; init; }

    /// <summary>Seconds.</summary>
    public required double Seconds { get; init; }

    /// <summary>Loop passes.</summary>
    public required int LoopPasses { get; init; }

    /// <summary>Best trainer speed, or null.</summary>
    public required double? BestTrainerPercent { get; init; }

    /// <summary>Last session with this section.</summary>
    public required DateTimeOffset LastPractisedAt { get; init; }
}
