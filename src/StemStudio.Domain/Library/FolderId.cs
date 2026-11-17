using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace StemStudio.Domain.Library;

/// <summary>
/// Derived folder ids (feature 022, research R-03): <c>ar|al|so</c> + 16 lower-case hex of SHA-256 over
/// <c>"&lt;prefix&gt;\u001f" + keys joined by "\u001f"</c> (UTF-8). Stable and URL-safe.
/// </summary>
public static partial class FolderId
{
    private const char Separator = '\u001f';

    /// <summary>The id of an artist folder.</summary>
    /// <param name="artistKey">Artist key.</param>
    /// <returns>The id.</returns>
    public static string Artist(string artistKey) => Of("ar", artistKey);

    /// <summary>The id of an album folder.</summary>
    /// <param name="artistKey">Artist key.</param>
    /// <param name="albumKey">Album key.</param>
    /// <returns>The id.</returns>
    public static string Album(string artistKey, string albumKey) => Of("al", artistKey, albumKey);

    /// <summary>The id of a song folder.</summary>
    /// <param name="artistKey">Artist key.</param>
    /// <param name="albumKey">Album key.</param>
    /// <param name="songKey">Song key.</param>
    /// <returns>The id.</returns>
    public static string Song(string artistKey, string albumKey, string songKey) => Of("so", artistKey, albumKey, songKey);

    /// <summary>Validates an id (<c>^(ar|al|so)[0-9a-f]{16}$</c>) and gives its level.</summary>
    /// <param name="id">Candidate id.</param>
    /// <param name="level">The level when valid.</param>
    /// <returns>True when well-formed.</returns>
    public static bool TryParse(string? id, out FolderLevel level)
    {
        level = default;
        if (id is null || !Pattern().IsMatch(id))
        {
            return false;
        }

        level = id[..2] switch
        {
            "ar" => FolderLevel.Artist,
            "al" => FolderLevel.Album,
            _ => FolderLevel.Song,
        };
        return true;
    }

    private static string Of(string prefix, params string[] keys)
    {
        foreach (var key in keys)
        {
            ArgumentNullException.ThrowIfNull(key);
        }

        var text = prefix + Separator + string.Join(Separator, keys);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return prefix + Convert.ToHexStringLower(hash.AsSpan(0, 8));
    }

    [GeneratedRegex("^(ar|al|so)[0-9a-f]{16}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Pattern();
}
