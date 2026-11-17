using System.Text.RegularExpressions;

namespace StemStudio.Domain.Tracks;

/// <summary>
/// Recognises the stored name of an upload used as a title (feature 022 hash-title guard). The worker only sees the
/// stored, hashed upload file (<c>&lt;sha256&gt;&lt;ext&gt;</c>) and, before the 404c8031 fix, reported its name as lookup title
/// of an untagged upload. Such a title is treated as absent so the track's own title names the song folder. Pure.
/// </summary>
public static partial class StoredUploadName
{
    /// <summary>True when <paramref name="title"/> is the stored upload name rather than a real title: the stem or full name
    /// of <paramref name="uploadFileName"/>, or a pure hex hash of 32-64 characters with an optional extension. Shorter hex
    /// strings ("deadbeef") are real titles.</summary>
    /// <param name="title">A (lookup) title, or null.</param>
    /// <param name="uploadFileName">The track's stored upload file name, or null (not an upload).</param>
    /// <returns>True when the title must be ignored.</returns>
    public static bool IsStoredName(string? title, string? uploadFileName)
    {
        var trimmed = title?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return false;
        }

        return HexHash().IsMatch(trimmed) || (uploadFileName is not null
            && (string.Equals(trimmed, uploadFileName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(trimmed, Path.GetFileNameWithoutExtension(uploadFileName), StringComparison.OrdinalIgnoreCase)));
    }

    [GeneratedRegex(@"\A[0-9a-fA-F]{32,64}(\.[A-Za-z0-9]{1,10})?\z", RegexOptions.CultureInvariant)]
    private static partial Regex HexHash();
}
