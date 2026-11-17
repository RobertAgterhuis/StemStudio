namespace StemStudio.Contracts.LyricAssist;

/// <summary>Rhythm target of a sheet (schema <c>RhythmTarget</c>); target syllables per line = beats x bars x syllables per beat.</summary>
public sealed record RhythmTargetDto
{
    /// <summary>Tempo, 40-220.</summary>
    public required int Bpm { get; init; }

    /// <summary>Beats per bar, 2-7.</summary>
    public required int BeatsPerBar { get; init; }

    /// <summary>Bars per line, 1-8.</summary>
    public required int BarsPerLine { get; init; }

    /// <summary>Syllables per beat, 0.5-4 in steps of 0.25.</summary>
    public required double SyllablesPerBeat { get; init; }
}
