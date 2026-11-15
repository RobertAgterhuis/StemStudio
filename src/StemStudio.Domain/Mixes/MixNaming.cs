namespace StemStudio.Domain.Mixes;

/// <summary>Name normalisation and copy naming for mixes.</summary>
public static class MixNaming
{
    /// <summary>The comparison form of a name: trimmed and upper-case (invariant).</summary>
    /// <param name="name">Name.</param>
    /// <returns>The normalised name.</returns>
    public static string Normalize(string name) => CopyNames.Normalize(name);

    /// <summary>
    /// The first free copy name: "&lt;name&gt; (copy)", then "&lt;name&gt; (copy 2)", ...; the base is cut so the
    /// result has at most 100 characters.
    /// </summary>
    /// <param name="name">Name of the original.</param>
    /// <param name="takenNames">Names already used for the track (any case).</param>
    /// <returns>The copy name.</returns>
    public static string CopyName(string name, IEnumerable<string> takenNames) => CopyNames.Next(name, takenNames, MixSettings.MaxNameLength);
}
