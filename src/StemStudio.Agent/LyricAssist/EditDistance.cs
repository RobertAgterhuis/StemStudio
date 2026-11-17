namespace StemStudio.Agent.LyricAssist;

/// <summary>Levenshtein distance of two short texts (at most 400 characters each, the proposal limit).</summary>
internal static class EditDistance
{
    /// <summary>The number of single-character insertions, deletions and substitutions that turn one text into the other.</summary>
    /// <param name="from">First text.</param>
    /// <param name="to">Second text.</param>
    /// <returns>The distance.</returns>
    public static int Of(string from, string to)
    {
        var previous = Enumerable.Range(0, to.Length + 1).ToArray();
        for (var i = 1; i <= from.Length; i++)
        {
            var current = new int[to.Length + 1];
            current[0] = i;
            for (var j = 1; j <= to.Length; j++)
            {
                var cost = from[i - 1] == to[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }

            previous = current;
        }

        return previous[to.Length];
    }
}
