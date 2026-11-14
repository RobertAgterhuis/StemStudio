using System.Text.RegularExpressions;

namespace StemStudio.Domain.Tracks;

/// <summary>Identifier formats per source type (data-model.md, SourceKey parsing).</summary>
/// <remarks>
/// Patterns are anchored with <c>\A</c> and <c>\z</c>: in .NET <c>$</c> also matches before a trailing
/// newline, which would let <c>"id\n"</c> through into keys and worker URLs.
/// </remarks>
internal static partial class SourceIds
{
    /// <summary>Checks an identifier against the format of its source type.</summary>
    /// <param name="type">Source type.</param>
    /// <param name="id">Identifier.</param>
    /// <returns>True when valid.</returns>
    public static bool IsValid(SourceType type, string id) => type switch
    {
        SourceType.Youtube => YoutubeId().IsMatch(id),
        SourceType.Spotify => SpotifyId().IsMatch(id),
        _ => Sha256Hex().IsMatch(id),
    };

    [GeneratedRegex(@"\A[A-Za-z0-9_-]{11}\z", RegexOptions.CultureInvariant)]
    private static partial Regex YoutubeId();

    [GeneratedRegex(@"\A[A-Za-z0-9]{22}\z", RegexOptions.CultureInvariant)]
    private static partial Regex SpotifyId();

    [GeneratedRegex(@"\A[0-9a-f]{64}\z", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256Hex();
}
