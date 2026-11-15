namespace StemStudio.Domain.Tracks;

/// <summary>Lifecycle state of a track; see data-model.md for the allowed transitions.</summary>
public enum TrackStatus
{
    /// <summary>Created, waiting for the worker.</summary>
    Queued,

    /// <summary>The worker is fetching the source audio.</summary>
    Downloading,

    /// <summary>The worker is separating stems.</summary>
    Separating,

    /// <summary>The worker is detecting the beat grid.</summary>
    Analysing,

    /// <summary>Terminal: stems and analysis stored by the API.</summary>
    Completed,

    /// <summary>Terminal: processing failed; may be retried.</summary>
    Failed,
}
