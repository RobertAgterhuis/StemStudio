using System.Globalization;

namespace StemStudio.Domain;

/// <summary>"&lt;name&gt; (copy)" naming shared by mixes and styles.</summary>
internal static class CopyNames
{
    /// <summary>Upper-case trimmed form used for case-insensitive uniqueness.</summary>
    /// <param name="name">The name.</param>
    /// <returns>The normalised name.</returns>
    public static string Normalize(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return name.Trim().ToUpperInvariant();
    }

    /// <summary>"&lt;name&gt; (copy)", then "(copy 2)", ... until free; the base is shortened to fit the limit.</summary>
    /// <param name="name">The name to copy.</param>
    /// <param name="takenNames">Names already in use.</param>
    /// <param name="maxLength">Longest allowed name.</param>
    /// <returns>A free name.</returns>
    public static string Next(string name, IEnumerable<string> takenNames, int maxLength)
    {
        var taken = takenNames.Select(Normalize).ToHashSet(StringComparer.Ordinal);
        var baseName = name.Trim();
        for (var number = 1; ; number++)
        {
            var candidate = Candidate(baseName, number, maxLength);
            if (!taken.Contains(Normalize(candidate)))
            {
                return candidate;
            }
        }
    }

    private static string Candidate(string baseName, int number, int maxLength)
    {
        var suffix = number == 1 ? " (copy)" : string.Create(CultureInfo.InvariantCulture, $" (copy {number})");
        var room = maxLength - suffix.Length;
        var cut = baseName.Length <= room ? baseName : baseName[..room].TrimEnd();
        return cut + suffix;
    }
}
