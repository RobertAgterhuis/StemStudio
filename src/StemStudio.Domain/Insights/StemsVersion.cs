using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace StemStudio.Domain.Insights;

/// <summary>One stem file as the quality report sees it: its channel, size and last write time.</summary>
/// <param name="Channel">ChannelId.</param>
/// <param name="SizeBytes">Size in bytes.</param>
/// <param name="LastWriteUtc">Last write time (UTC).</param>
public sealed record StemFileStamp(string Channel, long SizeBytes, DateTime LastWriteUtc);

/// <summary>
/// The version of a track's stem files (data-model 5.1 <c>StemsVersion</c>): a hash of the channels, sizes and write times,
/// independent of their order. A report whose version differs from the current files is outdated.
/// </summary>
public static class StemsVersion
{
    /// <summary>Length of the version text (hex).</summary>
    public const int Length = 32;

    /// <summary>The version of a set of stem files.</summary>
    /// <param name="stamps">The files.</param>
    /// <returns>32 lower-case hex characters.</returns>
    public static string Of(IEnumerable<StemFileStamp> stamps)
    {
        ArgumentNullException.ThrowIfNull(stamps);
        var lines = stamps.OrderBy(stamp => stamp.Channel, StringComparer.Ordinal)
            .Select(stamp => string.Create(CultureInfo.InvariantCulture, $"{stamp.Channel}|{stamp.SizeBytes}|{stamp.LastWriteUtc.Ticks}"));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', lines)));
        return Convert.ToHexStringLower(hash)[..Length];
    }
}
