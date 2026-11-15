namespace StemStudio.Domain.Mixes;

/// <summary>Mixer settings of one stem channel.</summary>
public sealed record ChannelSettings
{
    /// <summary>Default fader volume (80 %).</summary>
    public const double DefaultVolume = 0.8;

    /// <summary>Stem name; must exist on the track.</summary>
    public required string Stem { get; init; }

    /// <summary>Fader 0..1.</summary>
    public double Volume { get; init; } = DefaultVolume;

    /// <summary>Pan -1 (left) .. 1 (right).</summary>
    public double Pan { get; init; }

    /// <summary>Muted.</summary>
    public bool Mute { get; init; }

    /// <summary>Soloed.</summary>
    public bool Solo { get; init; }

    /// <summary>The default settings of a stem without an entry.</summary>
    /// <param name="stem">Stem name.</param>
    /// <returns>Volume 0.8, centre, not muted or soloed.</returns>
    public static ChannelSettings Default(string stem) => new() { Stem = stem };

    /// <summary>Writes the violations of the value ranges into a collector.</summary>
    /// <param name="errors">Collector positioned at this channel.</param>
    internal void ValidateRanges(ValidationErrors errors)
    {
        errors.Range("volume", Volume, Bounds.Unit);
        errors.Range("pan", Pan, Bounds.Pan);
    }
}
