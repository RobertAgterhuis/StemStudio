using System.Globalization;

namespace StemStudio.Web.Services;

/// <summary>Formats seconds for display.</summary>
public static class TimeText
{
    /// <summary>Formats a duration as <c>m:ss</c>.</summary>
    /// <param name="seconds">Seconds, or null when unknown.</param>
    /// <returns>For example "3:33", or "-" when unknown.</returns>
    public static string Duration(double? seconds)
    {
        if (seconds is not { } value)
        {
            return "-";
        }

        var whole = (int)Math.Floor(value);
        return string.Create(CultureInfo.InvariantCulture, $"{whole / 60}:{whole % 60:00}");
    }

    /// <summary>Formats a transport position as <c>m:ss.t</c>.</summary>
    /// <param name="seconds">Position in seconds (negative values clamp to 0).</param>
    /// <returns>For example "1:15.0".</returns>
    public static string Position(double seconds)
    {
        var tenths = (int)Math.Floor(Math.Max(0, seconds) * 10);
        var whole = tenths / 10;
        return string.Create(CultureInfo.InvariantCulture, $"{whole / 60}:{whole % 60:00}.{tenths % 10}");
    }
}
