namespace StemStudio.Domain.Mixes;

/// <summary>Fader, pan, mute and solo of one mixer channel; shared by stem and detail stem channels.</summary>
public abstract record FaderSettings
{
    /// <summary>Default fader volume (80 %).</summary>
    public const double DefaultVolume = 0.8;

    /// <summary>Fader 0..1.</summary>
    public double Volume { get; init; } = DefaultVolume;

    /// <summary>Pan -1 (left) .. 1 (right).</summary>
    public double Pan { get; init; }

    /// <summary>Muted.</summary>
    public bool Mute { get; init; }

    /// <summary>Soloed.</summary>
    public bool Solo { get; init; }

    /// <summary>Writes the violations of the value ranges into a collector.</summary>
    /// <param name="errors">Collector positioned at this channel.</param>
    internal void ValidateRanges(ValidationErrors errors)
    {
        errors.Range("volume", Volume, Bounds.Unit);
        errors.Range("pan", Pan, Bounds.Pan);
    }
}
