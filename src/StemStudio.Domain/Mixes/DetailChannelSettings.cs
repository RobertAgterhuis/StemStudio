namespace StemStudio.Domain.Mixes;

/// <summary>Mixer settings of one detail stem channel (feature 002, FR-015).</summary>
public sealed record DetailChannelSettings : FaderSettings
{
    /// <summary>Parent stem; must be split on the track.</summary>
    public required string Parent { get; init; }

    /// <summary>Detail stem name; must exist on the track under <see cref="Parent"/>.</summary>
    public required string Stem { get; init; }

    /// <summary>The default settings of a detail stem without an entry.</summary>
    /// <param name="parent">Parent stem name.</param>
    /// <param name="stem">Detail stem name.</param>
    /// <returns>Volume 0.8, centre, not muted or soloed.</returns>
    public static DetailChannelSettings Default(string parent, string stem) => new() { Parent = parent, Stem = stem };
}
