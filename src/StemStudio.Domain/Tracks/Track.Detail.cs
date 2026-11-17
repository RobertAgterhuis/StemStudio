using StemStudio.Domain.Tracks.Detail;

namespace StemStudio.Domain.Tracks;

/// <summary>Detail phase of a track (feature 002, data-model.md 3.1 and 4.1). Never changes the standard result.</summary>
public sealed partial class Track
{
    /// <summary>Failure text used when the worker gives no reason.</summary>
    public const string DetailFailedDefault = "Detail separation failed.";

    /// <summary>The detail phase, or null when it was never requested (status "none").</summary>
    public DetailPhase? DetailPhase { get; private set; }

    /// <summary>True while the detail phase is queued or running.</summary>
    public bool IsDetailActive => DetailPhase?.Status is DetailPhaseStatus.Queued or DetailPhaseStatus.Running;

    /// <summary>
    /// True when the track is completed and the detail phase is none, failed or cancelled, or completed with removed files
    /// (feature 013).
    /// </summary>
    public bool CanStartDetail =>
        Status == TrackStatus.Completed
        && (DetailPhase?.Status is null or DetailPhaseStatus.Failed or DetailPhaseStatus.Cancelled || DetailPhase.FilesRemovedAt is not null);

    /// <summary>True when the detail phase is completed and its stem files are present (feature 013).</summary>
    public bool HasDetailFiles => DetailPhase is { Status: DetailPhaseStatus.Completed, FilesRemovedAt: null };

    /// <summary>True when the track may be deleted: terminal and no detail phase queued or running (FR-016).</summary>
    public bool CanDelete => IsTerminal && !IsDetailActive;

    /// <summary>Starts a fresh detail phase: queued, progress 0, no worker job yet.</summary>
    /// <param name="nowUtc">Current time (UTC).</param>
    /// <exception cref="InvalidOperationException"><see cref="CanStartDetail"/> is false.</exception>
    public void RequestDetail(DateTime nowUtc)
    {
        if (!CanStartDetail)
        {
            throw new InvalidOperationException($"The detail phase of track {Id} cannot be started now.");
        }

        var removed = DetailPhase?.FilesRemovedAt;
        SetDetail(new DetailPhase { Status = DetailPhaseStatus.Queued, Stage = "Queued", FilesRemovedAt = removed, RequestedAt = nowUtc, UpdatedAt = nowUtc }, nowUtc);
    }

    /// <summary>Records that the detail stem files of the completed phase were removed (feature 013, data-model 5.3); names stay.</summary>
    /// <param name="nowUtc">Current time (UTC).</param>
    /// <exception cref="InvalidOperationException">The phase is not completed.</exception>
    public void RemoveDetailFiles(DateTime nowUtc)
    {
        if (DetailPhase is not { Status: DetailPhaseStatus.Completed } phase)
        {
            throw new InvalidOperationException($"Track {Id} has no completed detail phase.");
        }

        SetDetail(phase with { FilesRemovedAt = nowUtc, UpdatedAt = nowUtc }, nowUtc);
    }

    /// <summary>Records the worker detail job of the active phase.</summary>
    /// <param name="workerDetailJobId">Job ID (32 lower-case hex characters).</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    public void AssignDetailJob(string workerDetailJobId, DateTime nowUtc)
    {
        if (!JobIdPattern().IsMatch(workerDetailJobId))
        {
            throw new ArgumentException($"'{workerDetailJobId}' is not a worker job ID.", nameof(workerDetailJobId));
        }

        SetDetail(RequireActiveDetail() with { WorkerDetailJobId = workerDetailJobId, UpdatedAt = nowUtc }, nowUtc);
    }

    /// <summary>Applies a worker report; ignored without an active phase or for backward states; progress never decreases.</summary>
    /// <param name="progress">Report for queued or running.</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    public void ApplyDetailProgress(DetailProgress progress, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(progress);
        if (progress.Status is not (DetailPhaseStatus.Queued or DetailPhaseStatus.Running))
        {
            throw new ArgumentException("Use CompleteDetail, FailDetail or CancelDetail for terminal states.", nameof(progress));
        }

        if (IsDetailActive && progress.Status >= DetailPhase!.Status)
        {
            SetDetail(Advance(DetailPhase, progress, nowUtc), nowUtc);
        }
    }

    /// <summary>Completes the active phase with validated outcomes (after every detail stem file was stored).</summary>
    /// <param name="parents">One outcome per standard stem.</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    /// <exception cref="DomainValidationException">The outcomes violate a rule; the phase is unchanged.</exception>
    public void CompleteDetail(IReadOnlyList<ParentOutcome> parents, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(parents);
        var phase = RequireActiveDetail();
        DetailCompletionRules.Validate(parents, _stems.Select(stem => stem.Name).ToList());
        var ordered = parents.OrderBy(parent => StemCatalog.Order(parent.Parent)).Select(InDisplayOrder).ToList();
        SetDetail(Finish(phase, DetailPhaseStatus.Completed, null, nowUtc) with { Progress = 100, Parents = ordered, FilesRemovedAt = null }, nowUtc);
    }

    /// <summary>Fails the active phase with a readable reason; progress is kept.</summary>
    /// <param name="error">Reason (blank becomes <see cref="DetailFailedDefault"/>, cut to 1000 characters).</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    public void FailDetail(string error, DateTime nowUtc)
    {
        var phase = RequireActiveDetail();
        var reason = Text.TrimToLength(error, MaxErrorLength) ?? DetailFailedDefault;
        SetDetail(Finish(phase, DetailPhaseStatus.Failed, reason, nowUtc), nowUtc);
    }

    /// <summary>Cancels the active phase.</summary>
    /// <param name="nowUtc">Current time (UTC).</param>
    public void CancelDetail(DateTime nowUtc) => SetDetail(Finish(RequireActiveDetail(), DetailPhaseStatus.Cancelled, null, nowUtc), nowUtc);

    private static DetailPhase Advance(DetailPhase phase, DetailProgress progress, DateTime nowUtc) => phase with
    {
        Status = progress.Status,
        Stage = Text.TrimToLength(progress.Stage, MaxStageLength) ?? phase.Stage,
        CurrentParent = DetailCatalog.IsParent(progress.CurrentParent) ? progress.CurrentParent : null,
        Progress = Math.Max(phase.Progress, Math.Clamp(progress.Progress, 0, 100)),
        UpdatedAt = nowUtc,
    };

    private static DetailPhase Finish(DetailPhase phase, DetailPhaseStatus status, string? error, DateTime nowUtc) => phase with
    {
        Status = status,
        Stage = status.ToString(),
        Error = error,
        CurrentParent = null,
        UpdatedAt = nowUtc,
    };

    private static ParentOutcome InDisplayOrder(ParentOutcome parent) =>
        parent with { Stems = parent.Stems.OrderBy(stem => DetailCatalog.Order(parent.Parent, stem.Name)).ToList() };

    private DetailPhase RequireActiveDetail() =>
        IsDetailActive ? DetailPhase! : throw new InvalidOperationException($"Track {Id} has no queued or running detail phase.");

    private void SetDetail(DetailPhase phase, DateTime nowUtc) => (DetailPhase, UpdatedAt) = (phase, nowUtc);
}
