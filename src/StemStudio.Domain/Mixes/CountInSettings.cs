namespace StemStudio.Domain.Mixes;

/// <summary>Count-in of a mix (feature 012): metronome clicks before the music.</summary>
public sealed record CountInSettings
{
    private static readonly Bounds BarsBounds = new(0, 2);

    /// <summary>No count-in.</summary>
    public static CountInSettings Default { get; } = new();

    /// <summary>Counted bars 0..2; 0 = off.</summary>
    public int Bars { get; init; }

    /// <summary>Also before every pass of an enabled loop.</summary>
    public bool EveryPass { get; init; }

    /// <summary>Writes the violations into a collector.</summary>
    /// <param name="errors">Collector positioned at the count-in.</param>
    internal void Validate(ValidationErrors errors) => errors.Range("bars", Bars, BarsBounds);
}
