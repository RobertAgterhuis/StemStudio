namespace StemStudio.Domain.Tracks.Detail;

/// <summary>
/// Immutable snapshot of the detail phase of a track (data-model.md 3.1). Only <see cref="Track"/> creates new
/// snapshots through its detail transitions; the rules are enforced there.
/// </summary>
public sealed record DetailPhase
{
    /// <summary>Current status.</summary>
    public required DetailPhaseStatus Status { get; init; }

    /// <summary>Human-readable stage (&lt;= 200 characters).</summary>
    public string? Stage { get; init; }

    /// <summary>Parent stem being split while running, or null.</summary>
    public string? CurrentParent { get; init; }

    /// <summary>Progress 0..100; never decreases within one detail job.</summary>
    public int Progress { get; init; }

    /// <summary>Failure reason; set iff <see cref="Status"/> is failed (&lt;= 1000 characters).</summary>
    public string? Error { get; init; }

    /// <summary>Worker detail job (32 lower-case hex characters); null while the submission is pending.</summary>
    public string? WorkerDetailJobId { get; init; }

    /// <summary>One outcome per standard stem once completed, in standard display order; empty otherwise.</summary>
    public IReadOnlyList<ParentOutcome> Parents { get; init; } = [];

    /// <summary>Where the detail steps ran (feature 003); null before the first parent step, for cached results and older phases.</summary>
    public JobDevice? Device { get; init; }

    /// <summary>
    /// When the detail stems were removed by a clean-up or left out of a restored backup (feature 013, UTC); null while the
    /// files are present. Kept while the phase is started again and cleared when it completes.
    /// </summary>
    public DateTime? FilesRemovedAt { get; init; }

    /// <summary>Time the phase was (re)started (UTC).</summary>
    public required DateTime RequestedAt { get; init; }

    /// <summary>Last change of the phase (UTC).</summary>
    public required DateTime UpdatedAt { get; init; }
}
