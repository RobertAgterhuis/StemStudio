namespace StemStudio.Domain.Mixes;

/// <summary>Mixer settings of one stem channel.</summary>
public sealed record ChannelSettings : FaderSettings
{
    /// <summary>Stem name; must exist on the track.</summary>
    public required string Stem { get; init; }

    /// <summary>The default settings of a stem without an entry.</summary>
    /// <param name="stem">Stem name.</param>
    /// <returns>Volume 0.8, centre, not muted or soloed.</returns>
    public static ChannelSettings Default(string stem) => new() { Stem = stem };
}
