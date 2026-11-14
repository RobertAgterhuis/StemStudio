using System.Globalization;

namespace StemStudio.Domain.Mixes;

/// <summary>Name normalisation and copy naming for mixes.</summary>
public static class MixNaming
{
    /// <summary>The comparison form of a name: trimmed and upper-case (invariant).</summary>
    /// <param name="name">Name.</param>
    /// <returns>The normalised name.</returns>
    public static string Normalize(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return name.Trim().ToUpperInvariant();
    }

    /// <summary>
    /// The first free copy name: "&lt;name&gt; (copy)", then "&lt;name&gt; (copy 2)", ...; the base is cut so the
    /// result has at most 100 characters.
    /// </summary>
    /// <param name="name">Name of the original.</param>
    /// <param name="takenNames">Names already used for the track (any case).</param>
    /// <returns>The copy name.</returns>
    public static string CopyName(string name, IEnumerable<string> takenNames)
    {
        var taken = takenNames.Select(Normalize).ToHashSet(StringComparer.Ordinal);
        var baseName = name.Trim();
        for (var number = 1; ; number++)
        {
            var candidate = Candidate(baseName, number);
            if (!taken.Contains(Normalize(candidate)))
            {
                return candidate;
            }
        }
    }

    private static string Candidate(string baseName, int number)
    {
        var suffix = number == 1 ? " (copy)" : string.Create(CultureInfo.InvariantCulture, $" (copy {number})");
        var room = MixSettings.MaxNameLength - suffix.Length;
        var cut = baseName.Length <= room ? baseName : baseName[..room].TrimEnd();
        return cut + suffix;
    }
}
