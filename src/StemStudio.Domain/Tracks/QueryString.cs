namespace StemStudio.Domain.Tracks;

/// <summary>Minimal URL query-string reader (no dependency on System.Web).</summary>
internal static class QueryString
{
    /// <summary>Gets the first value of a query parameter.</summary>
    /// <param name="query">The query part, with or without the leading <c>?</c>.</param>
    /// <param name="name">Parameter name (case-sensitive).</param>
    /// <returns>The unescaped value, an empty string for a key without <c>=</c>, or null when absent.</returns>
    public static string? Get(string query, string name)
    {
        foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = part.IndexOf('=', StringComparison.Ordinal);
            var key = separator < 0 ? part : part[..separator];
            if (key == name)
            {
                return separator < 0 ? string.Empty : Uri.UnescapeDataString(part[(separator + 1)..]);
            }
        }

        return null;
    }
}
