using System.Globalization;
using StemStudio.Contracts.LyricAssist;

namespace StemStudio.Web.Components.Lyrics.Assist;

/// <summary>The rhythm target form (FR-017): BPM 40-220, beats per bar 2-7, bars per line 1-8, syllables per beat 0.5-4 in 0.25 steps.</summary>
/// <param name="target">The stored target, or null.</param>
public sealed class RhythmForm(RhythmTargetDto? target)
{
    /// <summary>Tempo.</summary>
    public int Bpm { get; set; } = target?.Bpm ?? 92;

    /// <summary>Beats per bar.</summary>
    public int BeatsPerBar { get; set; } = target?.BeatsPerBar ?? 4;

    /// <summary>Bars per line.</summary>
    public int BarsPerLine { get; set; } = target?.BarsPerLine ?? 2;

    /// <summary>Syllables per beat.</summary>
    public double SyllablesPerBeat { get; set; } = target?.SyllablesPerBeat ?? 1.5;

    /// <summary>The problem of the last <see cref="ToTarget"/>, or null.</summary>
    public string? Problem { get; private set; }

    /// <summary>"2.6 words per second at 1:24".</summary>
    /// <param name="wordsPerSecond">The density.</param>
    /// <param name="seconds">The target duration, or null.</param>
    /// <returns>The text.</returns>
    public static string DensityText(double wordsPerSecond, double? seconds)
    {
        var rate = wordsPerSecond.ToString("0.0", CultureInfo.InvariantCulture);
        var at = seconds is { } s ? $" at {(int)(s / 60)}:{((int)s % 60).ToString("00", CultureInfo.InvariantCulture)}" : string.Empty;
        var warn = wordsPerSecond > LyricAssistLimits.DensityWarnWordsPerSecond ? " - hard to sing, more than 4 words per second" : string.Empty;
        return $"{rate} words per second{at}{warn}";
    }

    /// <summary>The target, or null with <see cref="Problem"/> set when a value is out of range.</summary>
    /// <returns>The target.</returns>
    public RhythmTargetDto? ToTarget()
    {
        Problem = Valid() ? null : "Use BPM 40-220, 2-7 beats per bar, 1-8 bars per line and 0.5-4 syllables per beat in steps of 0.25.";
        return Problem is null ? new RhythmTargetDto { Bpm = Bpm, BeatsPerBar = BeatsPerBar, BarsPerLine = BarsPerLine, SyllablesPerBeat = SyllablesPerBeat } : null;
    }

    private bool Valid() =>
        Bpm is >= LyricAssistLimits.MinBpm and <= LyricAssistLimits.MaxBpm
        && BeatsPerBar is >= LyricAssistLimits.MinBeatsPerBar and <= LyricAssistLimits.MaxBeatsPerBar
        && BarsPerLine is >= LyricAssistLimits.MinBarsPerLine and <= LyricAssistLimits.MaxBarsPerLine
        && SyllablesPerBeat is >= LyricAssistLimits.MinSyllablesPerBeat and <= LyricAssistLimits.MaxSyllablesPerBeat
        && Math.Abs((SyllablesPerBeat / LyricAssistLimits.SyllablesPerBeatStep) - Math.Round(SyllablesPerBeat / LyricAssistLimits.SyllablesPerBeatStep)) < 1e-9;
}
