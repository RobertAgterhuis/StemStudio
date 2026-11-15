using System.Globalization;

namespace StemStudio.Web.Services;

/// <summary>Readable file sizes (binary units, labelled B, KB, MB, GB): "900 B", "1.5 KB", "312 MB".</summary>
public static class ByteSize
{
    private static readonly string[] Units = ["KB", "MB", "GB", "TB"];

    /// <summary>Formats a size: one decimal below 10 units, whole units from 10.</summary>
    /// <param name="bytes">The size in bytes.</param>
    /// <returns>The text.</returns>
    public static string Format(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes.ToString(CultureInfo.InvariantCulture)} B";
        }

        var value = (double)bytes;
        var unit = -1;
        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value.ToString(value < 10 ? "0.0" : "0", CultureInfo.InvariantCulture)} {Units[unit]}";
    }
}
