namespace StemStudio.Domain.Tracks;

/// <summary>Stem names and the stem set of each separation model (worker contract <c>ModelName</c>).</summary>
public static class StemCatalog
{
    /// <summary>The six stems of <c>htdemucs_6s</c> and <c>fake</c>, in display order.</summary>
    public static readonly IReadOnlyList<string> SixStem = ["vocals", "drums", "bass", "guitar", "piano", "other"];

    /// <summary>The four stems of the <c>htdemucs</c> fallback, in display order.</summary>
    public static readonly IReadOnlyList<string> FourStem = ["vocals", "drums", "bass", "other"];

    /// <summary>True for one of the six known stem names (case-sensitive, lower case).</summary>
    /// <param name="name">Stem name.</param>
    /// <returns>True when known.</returns>
    public static bool IsKnown(string name) => SixStem.Contains(name, StringComparer.Ordinal);

    /// <summary>Display position of a stem name.</summary>
    /// <param name="name">A known stem name.</param>
    /// <returns>The index in <see cref="SixStem"/>, or -1 when unknown.</returns>
    public static int Order(string name) => SixStem.ToList().IndexOf(name);

    /// <summary>The stem set a model produces.</summary>
    /// <param name="model">Model name.</param>
    /// <returns>The stem names, or null for an unknown model.</returns>
    public static IReadOnlyList<string>? ForModel(string model) => model switch
    {
        "htdemucs_6s" or "fake" => SixStem,
        "htdemucs" => FourStem,
        _ => null,
    };
}
