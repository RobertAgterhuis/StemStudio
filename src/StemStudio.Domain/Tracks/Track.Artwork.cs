namespace StemStudio.Domain.Tracks;

/// <summary>Pictures of a track (feature 005, data-model 3.1): the best automatic picture and an optional own picture.</summary>
public sealed partial class Track
{
    /// <summary>The picture shown: the user's picture when set, else <see cref="AutoArtwork"/>; null shows a placeholder.</summary>
    public TrackArtwork? Artwork { get; private set; }

    /// <summary>The best automatic picture by <see cref="ArtworkPrecedence"/>; kept while a user picture is shown.</summary>
    public TrackArtwork? AutoArtwork { get; private set; }

    /// <summary>True when the shown picture is the user's own.</summary>
    public bool HasUserArtwork => Artwork?.Source == ArtworkSource.User;

    /// <summary>True when <see cref="OfferArtwork"/> would take the automatic candidate (callers store its files first).</summary>
    /// <param name="candidate">The offered picture.</param>
    /// <returns>Whether it would replace <see cref="AutoArtwork"/>.</returns>
    public bool AcceptsArtwork(ArtworkCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        return candidate.Source != ArtworkSource.User && ArtworkPrecedence.Replaces(AutoArtwork, candidate);
    }

    /// <summary>Offers an automatic picture; it replaces <see cref="AutoArtwork"/> when it wins and is shown unless a user picture is set.</summary>
    /// <param name="candidate">The offered picture (never <see cref="ArtworkSource.User"/>).</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    /// <returns>True when the candidate was taken.</returns>
    /// <exception cref="ArgumentException">The candidate is a user picture (use <see cref="SetUserArtwork"/>).</exception>
    public bool OfferArtwork(ArtworkCandidate candidate, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (candidate.Source == ArtworkSource.User)
        {
            throw new ArgumentException("User pictures are set with SetUserArtwork.", nameof(candidate));
        }

        if (!AcceptsArtwork(candidate))
        {
            return false;
        }

        AutoArtwork = new TrackArtwork(candidate.Source, candidate.Sha256, nowUtc);
        (Artwork, UpdatedAt) = (HasUserArtwork ? Artwork : AutoArtwork, nowUtc);
        return true;
    }

    /// <summary>Shows the user's own picture (FR-013); the automatic picture is kept.</summary>
    /// <param name="sha256">SHA-256 of the normalised cover JPEG.</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    /// <exception cref="DomainValidationException">The hash is invalid.</exception>
    public void SetUserArtwork(string sha256, DateTime nowUtc) =>
        (Artwork, UpdatedAt) = (new TrackArtwork(ArtworkSource.User, sha256, nowUtc), nowUtc);

    /// <summary>Removes the user's picture and shows the automatic one (or none); a no-op without a user picture.</summary>
    /// <param name="nowUtc">Current time (UTC).</param>
    /// <returns>True when a user picture was removed.</returns>
    public bool ResetArtwork(DateTime nowUtc)
    {
        if (!HasUserArtwork)
        {
            return false;
        }

        (Artwork, UpdatedAt) = (AutoArtwork, nowUtc);
        return true;
    }
}
