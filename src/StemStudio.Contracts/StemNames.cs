namespace StemStudio.Contracts;

/// <summary>Stem names produced by the worker (htdemucs_6s; the htdemucs fallback yields vocals, drums, bass, other).</summary>
public static class StemNames
{
    /// <summary>Lead and backing vocals.</summary>
    public const string Vocals = "vocals";

    /// <summary>Drums and percussion.</summary>
    public const string Drums = "drums";

    /// <summary>Bass.</summary>
    public const string Bass = "bass";

    /// <summary>Guitar (six-stem model only).</summary>
    public const string Guitar = "guitar";

    /// <summary>Piano and keys (six-stem model only).</summary>
    public const string Piano = "piano";

    /// <summary>Everything else.</summary>
    public const string Other = "other";

    /// <summary>All stem names of the six-stem model, in display order.</summary>
    public static readonly IReadOnlyList<string> SixStem = [Vocals, Drums, Bass, Guitar, Piano, Other];

    /// <summary>All stem names of the four-stem fallback model, in display order.</summary>
    public static readonly IReadOnlyList<string> FourStem = [Vocals, Drums, Bass, Other];
}
