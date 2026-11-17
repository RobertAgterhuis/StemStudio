namespace StemStudio.Contracts.LyricAssist;

/// <summary>Syllables of a text (schema <c>SyllableReport</c>).</summary>
public sealed record SyllableReportDto
{
    /// <summary>Language.</summary>
    public required AssistLanguageDto Language { get; init; }

    /// <summary>Per line.</summary>
    public required IReadOnlyList<SyllableLineDto> Lines { get; init; }

    /// <summary>Duration of the lines at the rhythm target.</summary>
    public double? TargetDurationSeconds { get; init; }

    /// <summary>010 density at the target duration; the UI warns above 4.0.</summary>
    public double? WordsPerSecond { get; init; }
}
