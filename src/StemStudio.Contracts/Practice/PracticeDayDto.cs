namespace StemStudio.Contracts.Practice;

/// <summary>Practice time of one local day (<c>PracticeDay</c>) (backend-api 1.13.0, feature 014).</summary>
public sealed record PracticeDayDto
{
    /// <summary>Local date (UTC + offset).</summary>
    public required DateOnly Date { get; init; }

    /// <summary>Seconds practised that day.</summary>
    public required double Seconds { get; init; }
}
