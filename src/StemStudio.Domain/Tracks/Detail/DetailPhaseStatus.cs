namespace StemStudio.Domain.Tracks.Detail;

/// <summary>Status of a requested detail phase (data-model.md 4.1); a track without a phase is "none".</summary>
public enum DetailPhaseStatus
{
    /// <summary>Waiting for the worker; also while the submission is still pending.</summary>
    Queued,

    /// <summary>The worker is splitting a parent stem.</summary>
    Running,

    /// <summary>Terminal: per-parent outcomes and every detail stem file are stored.</summary>
    Completed,

    /// <summary>Terminal: see <see cref="DetailPhase.Error"/>; may be restarted.</summary>
    Failed,

    /// <summary>Terminal: cancelled by the user; may be restarted.</summary>
    Cancelled,
}
