namespace StemStudio.Domain;

/// <summary>String helpers for length-limited fields.</summary>
internal static class Text
{
    /// <summary>Trims the value and cuts it to the maximum length.</summary>
    /// <param name="value">Value.</param>
    /// <param name="maxLength">Maximum length.</param>
    /// <returns>The trimmed value, or null when it is null or blank.</returns>
    public static string? TrimToLength(string? value, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
