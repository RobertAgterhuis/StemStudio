namespace StemStudio.Web.Services;

/// <summary>Arrow, Home and End moves of a WAI-ARIA menu (APG menu pattern; wraps at both ends).</summary>
public static class MenuKeys
{
    /// <summary>The item index after a key, or null when the key does not move the focus.</summary>
    /// <param name="key">The <c>KeyboardEvent.key</c> value.</param>
    /// <param name="current">The focused item.</param>
    /// <param name="count">Number of items (at least 1).</param>
    /// <returns>The new index or null.</returns>
    public static int? Move(string key, int current, int count) => key switch
    {
        "ArrowDown" => (current + 1) % count,
        "ArrowUp" => (current + count - 1) % count,
        "Home" => 0,
        "End" => count - 1,
        _ => null,
    };
}
