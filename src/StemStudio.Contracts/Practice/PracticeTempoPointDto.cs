namespace StemStudio.Contracts.Practice;

/// <summary>Best trainer speed of a section on a day (<c>PracticeTempoPoint</c>) (backend-api 1.13.0, feature 014).</summary>
public sealed record PracticeTempoPointDto
{
    /// <summary>Local date.</summary>
    public required DateOnly Date { get; init; }

    /// <summary>Section id, or null.</summary>
    public required Guid? SectionId { get; init; }

    /// <summary>Label.</summary>
    public required string Label { get; init; }

    /// <summary>Speed in percent (50..120).</summary>
    public required double Percent { get; init; }
}
