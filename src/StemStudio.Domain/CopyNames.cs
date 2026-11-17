using System.Globalization;

namespace StemStudio.Domain;

/// <summary>"&lt;name&gt; (copy)" naming shared by mixes and styles; "(restored)" for merged items (013 research R-07).</summary>
internal static class CopyNames
{
    /// <summary>The word of a copy.</summary>
    public const string CopyWord = "copy";

    /// <summary>The word of an item a merge restore renamed.</summary>
    public const string RestoredWord = "restored";

    /// <summary>Upper-case trimmed form used for case-insensitive uniqueness.</summary>
    /// <param name="name">The name.</param>
    /// <returns>The normalised name.</returns>
    public static string Normalize(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return name.Trim().ToUpperInvariant();
    }

    /// <summary>"&lt;name&gt; (word)", then "(word 2)", ... until free; the base is shortened to fit the limit.</summary>
    /// <param name="name">The name to copy.</param>
    /// <param name="takenNames">Names already in use.</param>
    /// <param name="maxLength">Longest allowed name.</param>
    /// <param name="word">The suffix word (<see cref="CopyWord"/> or <see cref="RestoredWord"/>).</param>
    /// <returns>A free name.</returns>
    public static string Next(string name, IEnumerable<string> takenNames, int maxLength, string word = CopyWord)
    {
        var taken = takenNames.Select(Normalize).ToHashSet(StringComparer.Ordinal);
        var baseName = name.Trim();
        for (var number = 1; ; number++)
        {
            var candidate = Candidate(baseName, number, maxLength, word);
            if (!taken.Contains(Normalize(candidate)))
            {
                return candidate;
            }
        }
    }

    private static string Candidate(string baseName, int number, int maxLength, string word)
    {
        var suffix = number == 1 ? $" ({word})" : string.Create(CultureInfo.InvariantCulture, $" ({word} {number})");
        var room = maxLength - suffix.Length;
        var cut = baseName.Length <= room ? baseName : baseName[..room].TrimEnd();
        return cut + suffix;
    }
}
