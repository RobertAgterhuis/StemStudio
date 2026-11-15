namespace StemStudio.Domain.Tracks;

/// <summary>
/// The fixed picture precedence (FR-005, ADR-014 D2): user 0, Spotify 1, embedded 2, Cover Art Archive 3, YouTube 4.
/// A lower rank is better.
/// </summary>
public static class ArtworkPrecedence
{
    /// <summary>The rank of a source.</summary>
    /// <param name="source">The source.</param>
    /// <returns>0 (best) to 4.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The source is not defined.</exception>
    public static int Rank(ArtworkSource source) =>
        Enum.IsDefined(source) ? (int)source : throw new ArgumentOutOfRangeException(nameof(source), source, "Unknown artwork source.");

    /// <summary>True when the candidate replaces the current picture: none yet, a better rank, or the same rank with a new hash.</summary>
    /// <param name="current">The current picture, or null.</param>
    /// <param name="candidate">The offered picture.</param>
    /// <returns>Whether the candidate wins.</returns>
    public static bool Replaces(TrackArtwork? current, ArtworkCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (current is null)
        {
            return true;
        }

        var (offered, shown) = (Rank(candidate.Source), Rank(current.Source));
        return offered < shown || (offered == shown && candidate.Sha256 != current.Sha256);
    }
}
