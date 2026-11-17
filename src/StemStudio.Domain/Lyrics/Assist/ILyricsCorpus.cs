namespace StemStudio.Domain.Lyrics.Assist;

/// <summary>
/// The n-gram index over the library's 006 track lyrics (research R-07; built in memory by the API, refreshed on lyrics changes).
/// Keys are normalised: lower case, no diacritics, punctuation removed, single spaces. The guard rules live in <c>VerbatimGuard</c>.
/// </summary>
public interface ILyricsCorpus
{
    /// <summary>True when the 7-word shingle occurs in any library lyrics.</summary>
    /// <param name="shingle">Seven normalised words joined by single spaces.</param>
    /// <returns>True when found.</returns>
    bool ContainsShingle(string shingle);

    /// <summary>The tracks whose lyrics contain the full normalised line (lines of 5+ words are indexed).</summary>
    /// <param name="line">A normalised line.</param>
    /// <returns>The tracks; empty when none.</returns>
    IReadOnlyList<CorpusTrack> TracksWithLine(string line);
}
