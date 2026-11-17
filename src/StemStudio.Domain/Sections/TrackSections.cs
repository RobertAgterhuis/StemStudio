using System.Text.RegularExpressions;

namespace StemStudio.Domain.Sections;

/// <summary>
/// The section set of one track (data-model 3.1): detection status, origin, revision and the ordered sections.
/// </summary>
public sealed partial class TrackSections
{
    /// <summary>Longest stored failure text.</summary>
    public const int MaxErrorLength = 500;

    /// <summary>Failure text when the worker gave none.</summary>
    public const string DefaultFailure = "The sections could not be found.";

    private List<Section> _sections = [];

    private TrackSections()
    {
    }

    /// <summary>The track (also the key).</summary>
    public Guid TrackId { get; private set; }

    /// <summary>Detection status.</summary>
    public SectionSetStatus Status { get; private set; }

    /// <summary>Detected or edited.</summary>
    public SectionSetOrigin Origin { get; private set; }

    /// <summary>+1 on every replace (detected or edited).</summary>
    public int Revision { get; private set; }

    /// <summary>Analyser id of the last detection, or null.</summary>
    public string? Analyzer { get; private set; }

    /// <summary>Snap mode of the last detection, or null.</summary>
    public SectionSnap? Snap { get; private set; }

    /// <summary>Warnings of the last detection.</summary>
    public IReadOnlyList<SectionWarning> Warnings { get; private set; } = [];

    /// <summary>Worker job while queued or detecting.</summary>
    public string? WorkerJobId { get; private set; }

    /// <summary>Failure text when failed.</summary>
    public string? Error { get; private set; }

    /// <summary>Last change (UTC).</summary>
    public DateTime UpdatedAt { get; private set; }

    /// <summary>The sections in time order.</summary>
    public IReadOnlyList<Section> Sections => _sections;

    /// <summary>True while a detection is queued or running.</summary>
    public bool IsDetecting => Status is SectionSetStatus.Queued or SectionSetStatus.Detecting;

    /// <summary>A set that was never detected.</summary>
    /// <param name="trackId">The track.</param>
    /// <param name="nowUtc">Now.</param>
    /// <returns>The set with status <see cref="SectionSetStatus.None"/>.</returns>
    public static TrackSections Create(Guid trackId, DateTime nowUtc) => new() { TrackId = trackId, UpdatedAt = nowUtc };

    /// <summary>Whether a detection may start now (the track's own readiness is checked by the caller).</summary>
    /// <param name="overwrite">The user confirmed replacing edited sections.</param>
    /// <returns>The refusal, or <see cref="DetectRefusal.None"/>.</returns>
    public DetectRefusal CheckDetect(bool overwrite) => this switch
    {
        { IsDetecting: true } => DetectRefusal.DetectionRunning,
        { Origin: SectionSetOrigin.Edited } when !overwrite => DetectRefusal.SectionsEdited,
        _ => DetectRefusal.None,
    };

    /// <summary>Records the submitted worker job (status queued).</summary>
    /// <param name="workerJobId">Worker job id (32 lower-case hex digits).</param>
    /// <param name="nowUtc">Now.</param>
    public void StartDetection(string workerJobId, DateTime nowUtc)
    {
        if (!JobIdPattern().IsMatch(workerJobId))
        {
            throw new ArgumentException($"'{workerJobId}' is not a worker job ID.", nameof(workerJobId));
        }

        (Status, WorkerJobId, Error, UpdatedAt) = (SectionSetStatus.Queued, workerJobId, null, nowUtc);
    }

    /// <summary>The worker started the job.</summary>
    /// <param name="nowUtc">Now.</param>
    public void MarkDetecting(DateTime nowUtc)
    {
        if (Status == SectionSetStatus.Queued)
        {
            (Status, UpdatedAt) = (SectionSetStatus.Detecting, nowUtc);
        }
    }

    /// <summary>The submission failed: back to <see cref="SectionSetStatus.None"/> so the mixer triggers it again.</summary>
    /// <param name="nowUtc">Now.</param>
    public void ResetToNone(DateTime nowUtc) => (Status, WorkerJobId, UpdatedAt) = (SectionSetStatus.None, null, nowUtc);

    /// <summary>The detection failed.</summary>
    /// <param name="error">The worker's text, or null.</param>
    /// <param name="nowUtc">Now.</param>
    public void Fail(string? error, DateTime nowUtc)
    {
        Error = Text.TrimToLength(error, MaxErrorLength) ?? DefaultFailure;
        (Status, WorkerJobId, UpdatedAt) = (SectionSetStatus.Failed, null, nowUtc);
    }

    /// <summary>Replaces every section with the detected ones (new ids, origin detected, revision + 1).</summary>
    /// <param name="result">The completed job's result.</param>
    /// <param name="newId">Id source.</param>
    /// <param name="nowUtc">Now.</param>
    public void ApplyDetected(DetectionResult result, Func<Guid> newId, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(newId);
        _sections = result.Sections.Take(SectionRules.MaxSections)
            .Select((found, position) => Section.FromDetection(newId(), TrackId, position, found)).ToList();
        (Analyzer, Snap, Warnings) = (result.Analyzer, result.Snap, result.Warnings.ToList());
        Stored(SectionSetOrigin.Detected, nowUtc);
    }

    /// <summary>Replaces the list with the user's edit (kept ids stay, changed detected sections become user sections).</summary>
    /// <param name="update">Revision and the new list.</param>
    /// <param name="context">Track length and channels.</param>
    /// <param name="newId">Id source for new sections.</param>
    /// <param name="nowUtc">Now.</param>
    /// <exception cref="SectionsChangedException">The revision is not the stored one.</exception>
    /// <exception cref="DomainValidationException">A rule is broken.</exception>
    public void Replace(SectionSetUpdate update, SectionContext context, Func<Guid> newId, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(update);
        ArgumentNullException.ThrowIfNull(newId);
        if (update.Revision != Revision)
        {
            throw new SectionsChangedException();
        }

        var existing = _sections.ToDictionary(section => section.Id);
        var errors = SectionRules.Validate(update.Sections, context, existing.Keys.ToHashSet());
        if (errors.Count > 0)
        {
            throw new DomainValidationException(errors);
        }

        _sections = update.Sections.Select((input, position) => Merge(existing, input, position, newId)).ToList();
        Stored(SectionSetOrigin.Edited, nowUtc);
    }

    private Section Merge(Dictionary<Guid, Section> existing, SectionInput input, int position, Func<Guid> newId)
    {
        if (input.Id is { } id && existing.Remove(id, out var kept))
        {
            kept.Edit(position, input);
            return kept;
        }

        return Section.FromUser(newId(), TrackId, position, input);
    }

    private void Stored(SectionSetOrigin origin, DateTime nowUtc)
    {
        (Origin, Revision, Status) = (origin, Revision + 1, SectionSetStatus.Ready);
        (WorkerJobId, Error, UpdatedAt) = (null, null, nowUtc);
    }

    [GeneratedRegex(@"\A[0-9a-f]{32}\z", RegexOptions.CultureInvariant)]
    private static partial Regex JobIdPattern();
}
