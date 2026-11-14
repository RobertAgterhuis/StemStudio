using StemStudio.Domain.Tracks;

namespace StemStudio.Domain.Mixes;

/// <summary>Metronome settings of a mix (FR-014).</summary>
public sealed record MetronomeSettings
{
    private static readonly Bounds BpmBounds = new(30, 300);
    private static readonly Bounds OffsetBounds = new(-2000, 2000);
    private static readonly Bounds BeatsPerBarBounds = new(1, 12);

    /// <summary>Default click volume.</summary>
    public const double DefaultVolume = 0.7;

    /// <summary>Click on/off.</summary>
    public required bool Enabled { get; init; }

    /// <summary>Click volume 0..1.</summary>
    public double Volume { get; init; } = DefaultVolume;

    /// <summary>Manual tempo 30..300, or null to follow the beat grid.</summary>
    public double? BpmOverride { get; init; }

    /// <summary>Grid offset in milliseconds, -2000..2000.</summary>
    public int OffsetMs { get; init; }

    /// <summary>Beats per bar 1..12.</summary>
    public int BeatsPerBar { get; init; } = 4;

    /// <summary>The defaults for a track: enabled iff the grid is reliable, beats per bar from the grid or 4.</summary>
    /// <param name="grid">The track's beat grid, if any.</param>
    /// <returns>The default settings.</returns>
    public static MetronomeSettings DefaultFor(BeatGrid? grid) => new()
    {
        Enabled = grid?.Reliable ?? false,
        BeatsPerBar = grid?.BeatsPerBar ?? 4,
    };

    /// <summary>Writes the violations into a collector.</summary>
    /// <param name="errors">Collector positioned at the metronome.</param>
    internal void Validate(ValidationErrors errors)
    {
        errors.Range("volume", Volume, Bounds.Unit);
        errors.Check(BpmOverride is null || BpmBounds.Contains(BpmOverride.Value), "bpmOverride", BpmBounds.Message);
        errors.Range("offsetMs", OffsetMs, OffsetBounds);
        errors.Range("beatsPerBar", BeatsPerBar, BeatsPerBarBounds);
    }
}
