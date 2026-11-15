using System.Text.RegularExpressions;

namespace StemStudio.Domain.Tracks;

/// <summary>
/// Aggregate root for an imported song: source, processing state, stems and beat grid
/// (data-model.md section 1 and state machine 3.1).
/// </summary>
public sealed partial class Track
{
    /// <summary>Maximum title length.</summary>
    public const int MaxTitleLength = 500;

    /// <summary>Maximum artist length.</summary>
    public const int MaxArtistLength = 500;

    /// <summary>Maximum stage length.</summary>
    public const int MaxStageLength = 200;

    /// <summary>Maximum error length.</summary>
    public const int MaxErrorLength = 1000;

    /// <summary>Maximum duration in seconds (15 minutes).</summary>
    public const double MaxDurationSeconds = 900;

    private List<Stem> _stems = [];

    private Track()
    {
        Key = null!;
        Title = string.Empty;
    }

    /// <summary>Identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Source type.</summary>
    public SourceType SourceType { get; private set; }

    /// <summary>Cache identity (unique).</summary>
    public SourceKey Key { get; private set; }

    /// <summary>The URL as submitted (null for uploads).</summary>
    public string? SourceUrl { get; private set; }

    /// <summary>Title (1..500 characters).</summary>
    public string Title { get; private set; }

    /// <summary>Artist (&lt;= 500 characters).</summary>
    public string? Artist { get; private set; }

    /// <summary>Processing status.</summary>
    public TrackStatus Status { get; private set; }

    /// <summary>Human-readable stage (&lt;= 200 characters).</summary>
    public string? Stage { get; private set; }

    /// <summary>Progress 0..100; never decreases within one job.</summary>
    public int Progress { get; private set; }

    /// <summary>Failure reason; set iff <see cref="Status"/> is failed.</summary>
    public string? Error { get; private set; }

    /// <summary>Current worker job (32 lower-case hex characters).</summary>
    public string? WorkerJobId { get; private set; }

    /// <summary>Stored upload file name under <c>uploads/</c> (uploads only).</summary>
    public string? UploadFileName { get; private set; }

    /// <summary>Duration in seconds once completed.</summary>
    public double? DurationSeconds { get; private set; }

    /// <summary>Separation model once completed.</summary>
    public string? Model { get; private set; }

    /// <summary>Stored stems in display order; empty until completed.</summary>
    public IReadOnlyList<Stem> Stems => _stems;

    /// <summary>Beat analysis once completed.</summary>
    public BeatGrid? BeatGrid { get; private set; }

    /// <summary>Creation time (UTC).</summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>Last modification time (UTC).</summary>
    public DateTime UpdatedAt { get; private set; }

    /// <summary>True when completed or failed.</summary>
    public bool IsTerminal => Status is TrackStatus.Completed or TrackStatus.Failed;

    /// <summary>True when the track may be retried (failed only).</summary>
    public bool CanRetry => Status == TrackStatus.Failed;

    /// <summary>Creates a queued track for a YouTube or Spotify URL; the title is the URL.</summary>
    /// <param name="id">New identifier.</param>
    /// <param name="key">YouTube or Spotify key.</param>
    /// <param name="sourceUrl">The URL as submitted.</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    /// <returns>The track.</returns>
    public static Track FromUrl(Guid id, SourceKey key, string sourceUrl, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(key);
        RequireUrlKey(key, true);
        var url = sourceUrl.Trim();
        return Create(key, nowUtc, track => (track.Id, track.SourceUrl, track.Title) = (id, url, Text.TrimToLength(url, MaxTitleLength)!));
    }

    /// <summary>Creates a queued track for an uploaded file; the title is the file name without extension.</summary>
    /// <param name="id">New identifier.</param>
    /// <param name="key">Upload key.</param>
    /// <param name="upload">The stored upload.</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    /// <returns>The track.</returns>
    public static Track FromUpload(Guid id, SourceKey key, UploadedFile upload, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(upload);
        RequireUrlKey(key, false);
        var title = Text.TrimToLength(Path.GetFileNameWithoutExtension(upload.OriginalFileName), MaxTitleLength) ?? "Untitled upload";
        return Create(key, nowUtc, track => (track.Id, track.Title, track.UploadFileName) = (id, title, upload.StoredFileName));
    }

    /// <summary>Records the worker job that processes this track.</summary>
    /// <param name="workerJobId">Job ID (32 lower-case hex characters).</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    public void AssignJob(string workerJobId, DateTime nowUtc)
    {
        if (!JobIdPattern().IsMatch(workerJobId))
        {
            throw new ArgumentException($"'{workerJobId}' is not a worker job ID.", nameof(workerJobId));
        }

        RequireActive();
        WorkerJobId = workerJobId;
        UpdatedAt = nowUtc;
    }

    /// <summary>Applies a worker status report; backward states are ignored, progress never decreases.</summary>
    /// <param name="progress">Report for a non-terminal state.</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    public void ApplyProgress(TrackProgress progress, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(progress);
        if (progress.Status is TrackStatus.Completed or TrackStatus.Failed)
        {
            throw new ArgumentException("Use Complete or Fail for terminal states.", nameof(progress));
        }

        if (IsTerminal || progress.Status < Status)
        {
            return;
        }

        (Status, Stage) = (progress.Status, Text.TrimToLength(progress.Stage, MaxStageLength));
        Progress = Math.Max(Progress, Math.Clamp(progress.Progress, 0, 100));
        UpdatedAt = nowUtc;
    }

    /// <summary>Applies title and artist reported by the worker while the track is not terminal.</summary>
    /// <param name="metadata">Reported metadata.</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    public void ApplyMetadata(TrackMetadata metadata, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        var title = Text.TrimToLength(metadata.Title, MaxTitleLength) ?? Title;
        var artist = Text.TrimToLength(metadata.Artist, MaxArtistLength);
        if (IsTerminal || (title == Title && artist == Artist))
        {
            return;
        }

        (Title, Artist, UpdatedAt) = (title, artist, nowUtc);
    }

    /// <summary>Marks the track completed with its stored stems and analysis; progress becomes 100.</summary>
    /// <param name="completion">The validated result.</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    /// <exception cref="DomainValidationException">The result violates a rule (duration, model, stem set, beat grid).</exception>
    public void Complete(TrackCompletion completion, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(completion);
        RequireActive();
        TrackCompletionRules.Validate(completion);
        (DurationSeconds, Model, BeatGrid) = (completion.DurationSeconds, completion.Model, completion.BeatGrid);
        _stems = completion.Stems.OrderBy(stem => StemCatalog.Order(stem.Name)).ToList();
        SetState(TrackStatus.Completed, "Completed", null);
        Progress = 100;
        UpdatedAt = nowUtc;
    }

    /// <summary>Marks the track failed with a readable reason; progress is kept.</summary>
    /// <param name="error">Reason (blank becomes "Processing failed.", cut to 1000 characters).</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    public void Fail(string error, DateTime nowUtc)
    {
        RequireActive();
        SetState(TrackStatus.Failed, "Failed", Text.TrimToLength(error, MaxErrorLength) ?? "Processing failed.");
        UpdatedAt = nowUtc;
    }

    /// <summary>Puts a failed track back to queued for a new worker job.</summary>
    /// <param name="nowUtc">Current time (UTC).</param>
    public void Retry(DateTime nowUtc)
    {
        if (!CanRetry)
        {
            throw new InvalidOperationException("Only failed tracks can be retried.");
        }

        SetState(TrackStatus.Queued, "Queued", null);
        (Progress, WorkerJobId, Device, UpdatedAt) = (0, null, null, nowUtc);
    }

    private static Track Create(SourceKey key, DateTime nowUtc, Action<Track> init)
    {
        var track = new Track { Key = key, SourceType = key.Type, CreatedAt = nowUtc, UpdatedAt = nowUtc };
        track.SetState(TrackStatus.Queued, "Queued", null);
        init(track);
        return track;
    }

    private static void RequireUrlKey(SourceKey key, bool urlExpected)
    {
        if ((key.Type != SourceType.Upload) != urlExpected)
        {
            throw new ArgumentException($"Source key {key} does not match the import kind.", nameof(key));
        }
    }

    private void RequireActive()
    {
        if (IsTerminal)
        {
            throw new InvalidOperationException($"Track {Id} is already {Status}.");
        }
    }

    private void SetState(TrackStatus status, string stage, string? error) => (Status, Stage, Error) = (status, stage, error);

    [GeneratedRegex(@"\A[0-9a-f]{32}\z", RegexOptions.CultureInvariant)]
    private static partial Regex JobIdPattern();
}
