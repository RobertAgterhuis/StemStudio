using System.Globalization;

namespace StemStudio.Domain.Rendering;

/// <summary>The contents text of an export (data-model 4.3): "&lt;channels&gt;, &lt;raw|mixed&gt;[, m:ss-m:ss]".</summary>
public static class ExportContents
{
    /// <summary>Longest contents text.</summary>
    public const int MaxLength = 200;

    /// <summary>Adds ", m:ss-m:ss" for a range.</summary>
    /// <param name="contents">Contents without range.</param>
    /// <param name="range">The range, or null.</param>
    /// <returns>The text (at most 200 characters).</returns>
    public static string WithRange(string contents, ExportRange? range)
    {
        ArgumentNullException.ThrowIfNull(contents);
        var text = range is null ? contents : $"{contents}, {Time(range.StartSeconds)}-{Time(range.EndSeconds)}";
        return text.Length <= MaxLength ? text : text[..MaxLength];
    }

    private static string Time(double seconds)
    {
        var whole = (int)Math.Floor(seconds);
        return string.Create(CultureInfo.InvariantCulture, $"{whole / 60}:{whole % 60:00}");
    }
}
