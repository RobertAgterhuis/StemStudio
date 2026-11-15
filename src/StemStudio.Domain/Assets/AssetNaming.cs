using System.Globalization;

namespace StemStudio.Domain.Assets;

/// <summary>Display names and download file names of library items (data-model 4.4).</summary>
public static class AssetNaming
{
    /// <summary>Longest display name.</summary>
    public const int MaxNameLength = 120;

    /// <summary>Longest download file name before the extension.</summary>
    public const int MaxFileNameLength = 150;

    private const string Forbidden = "<>:\"/\\|?*";

    /// <summary>"&lt;track title&gt; - &lt;contents&gt;", cut to 120 characters, made unique with " (2)", " (3)", ... (case-insensitive).</summary>
    /// <param name="trackTitle">Track title.</param>
    /// <param name="contents">Contents text without the mode.</param>
    /// <param name="taken">Names already used.</param>
    /// <returns>The name.</returns>
    public static string DefaultName(string trackTitle, string contents, IEnumerable<string> taken)
    {
        ArgumentNullException.ThrowIfNull(taken);
        var used = taken.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var basis = $"{trackTitle.Trim()} - {contents}";
        var name = Cut(basis, string.Empty);
        for (var copy = 2; used.Contains(name); copy++)
        {
            name = Cut(basis, string.Create(CultureInfo.InvariantCulture, $" ({copy})"));
        }

        return name;
    }

    /// <summary>The validated display name.</summary>
    /// <param name="name">Requested name.</param>
    /// <returns>The trimmed name.</returns>
    /// <exception cref="DomainValidationException">Empty, longer than 120 characters or with control characters.</exception>
    public static string Validate(string? name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        var errors = new ValidationErrors();
        errors.Check(trimmed.Length is > 0 and <= MaxNameLength && !trimmed.Any(char.IsControl), "name", "Enter a name of 1 to 120 characters without control characters.");
        errors.ThrowIfAny();
        return trimmed;
    }

    /// <summary>The download file name: forbidden and control characters replaced by <c>_</c>, trimmed, cut to 150, plus the extension.</summary>
    /// <param name="name">Display name.</param>
    /// <param name="extension">Extension without dot.</param>
    /// <returns>The file name.</returns>
    public static string DownloadFileName(string name, string extension)
    {
        ArgumentNullException.ThrowIfNull(name);
        var safe = new string(name.Trim().Select(c => char.IsControl(c) || Forbidden.Contains(c, StringComparison.Ordinal) ? '_' : c).ToArray()).Trim();
        return $"{(safe.Length > MaxFileNameLength ? safe[..MaxFileNameLength] : safe)}.{extension}";
    }

    private static string Cut(string basis, string suffix) =>
        (basis.Length + suffix.Length > MaxNameLength ? basis[..(MaxNameLength - suffix.Length)] : basis) + suffix;
}
