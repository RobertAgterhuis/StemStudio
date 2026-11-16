namespace StemStudio.Domain.Tracks;

/// <summary>Origin of a track imported from a generated take (feature 009, data-model 3.3).</summary>
public sealed partial class Track
{
    /// <summary>The generation and take this track was imported from; null for imported songs.</summary>
    public TrackOrigin? Origin { get; private set; }

    /// <summary>Marks the track as generated; the first origin wins.</summary>
    /// <param name="origin">The origin.</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    /// <returns>True when the origin was set by this call.</returns>
    public bool MarkGenerated(TrackOrigin origin, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(origin);
        if (Origin is not null)
        {
            return false;
        }

        (Origin, UpdatedAt) = (origin, nowUtc);
        return true;
    }
}
