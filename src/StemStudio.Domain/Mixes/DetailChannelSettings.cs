namespace StemStudio.Domain.Mixes;

/// <summary>Mixer settings of one detail stem channel (feature 002, FR-015).</summary>
public sealed record DetailChannelSettings
{
    /// <summary>Parent stem; must be split on the track.</summary>
    public required string Parent { get; init; }

    /// <summary>Detail stem name; must exist on the track under <see cref="Parent"/>.</summary>
    public required string Stem { get; init; }

    /// <summary>Fader 0..1.</summary>
    public double Volume { get; init; } = ChannelSettings.DefaultVolume;

    /// <summary>Pan -1 (left) .. 1 (right).</summary>
    public double Pan { get; init; }

    /// <summary>Muted.</summary>
    public bool Mute { get; init; }

    /// <summary>Soloed.</summary>
    public bool Solo { get; init; }

    /// <summary>The default settings of a detail stem without an entry.</summary>
    /// <param name="parent">Parent stem name.</param>
    /// <param name="stem">Detail stem name.</param>
    /// <returns>Volume 0.8, centre, not muted or soloed.</returns>
    public static DetailChannelSettings Default(string parent, string stem) => new() { Parent = parent, Stem = stem };

    /// <summary>Writes the violations of the value ranges into a collector.</summary>
    /// <param name="errors">Collector positioned at this channel.</param>
    internal void ValidateRanges(ValidationErrors errors)
    {
        errors.Range("volume", Volume, Bounds.Unit);
        errors.Range("pan", Pan, Bounds.Pan);
    }
}
