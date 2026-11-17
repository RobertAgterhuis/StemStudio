using StemStudio.Domain.Scores;

namespace StemStudio.Domain.Assets;

/// <summary>
/// A library item: a file the app made, e.g. an exported stem or mix (feature 007, data-model 4.1 and status mapping
/// 4.5). Kept when its track is deleted.
/// </summary>
public sealed partial class Asset
{
    /// <summary>Longest error text.</summary>
    public const int MaxErrorLength = 500;

    /// <summary>Error text used when the worker gives no reason.</summary>
    public const string RenderFailedDefault = "Rendering failed.";

    private List<AssetFile> _files = [];

    private Asset()
    {
        Name = string.Empty;
        Provenance = null!;
    }

    /// <summary>Identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Kind.</summary>
    public AssetKind Kind { get; private set; }

    /// <summary>Display name (1..120 characters, not unique).</summary>
    public string Name { get; private set; }

    /// <summary>Status.</summary>
    public AssetStatus Status { get; private set; }

    /// <summary>Progress 0..100; never decreases within one render.</summary>
    public int Progress { get; private set; }

    /// <summary>Readable reason; set exactly when failed.</summary>
    public string? Error { get; private set; }

    /// <summary>The worker render job while queued or rendering.</summary>
    public string? WorkerJobId { get; private set; }

    /// <summary>Stored files; empty until ready.</summary>
    public IReadOnlyList<AssetFile> Files => _files;

    /// <summary>Audio facts once ready.</summary>
    public AudioFacts? Audio { get; private set; }

    /// <summary>Score facts of a ready score item (feature 008); null otherwise.</summary>
    public ScoreFacts? Score { get; private set; }

    /// <summary>Where the item came from.</summary>
    public Provenance Provenance { get; private set; }

    /// <summary>The source track (copied from the provenance for filtering).</summary>
    public Guid? TrackId { get; private set; }

    /// <summary>Sum of the file sizes.</summary>
    public long SizeBytes { get; private set; }

    /// <summary>Creation time (UTC).</summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>Last change (UTC).</summary>
    public DateTime UpdatedAt { get; private set; }

    /// <summary>True while queued or rendering.</summary>
    public bool IsActive => Status is AssetStatus.Queued or AssetStatus.Rendering;

    /// <summary>Creates a queued audio item.</summary>
    /// <param name="id">New identifier.</param>
    /// <param name="name">Display name (validated).</param>
    /// <param name="provenance">Where it came from.</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    /// <returns>The item.</returns>
    /// <exception cref="DomainValidationException">The name breaks a rule.</exception>
    public static Asset Create(Guid id, string name, Provenance provenance, DateTime nowUtc)
    {
        Require(provenance, provenance?.Recipe is not null, "An audio item needs an export recipe.");
        return New(id, AssetKind.Audio, name).Started(provenance!, nowUtc);
    }

    /// <summary>Creates a queued score item (feature 008).</summary>
    /// <param name="id">New identifier.</param>
    /// <param name="name">Display name (validated).</param>
    /// <param name="provenance">Where it came from, with its score recipe.</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    /// <returns>The item.</returns>
    /// <exception cref="DomainValidationException">The name breaks a rule.</exception>
    public static Asset CreateScore(Guid id, string name, Provenance provenance, DateTime nowUtc)
    {
        Require(provenance, provenance?.ScoreRecipe is not null, "A score item needs a score recipe.");
        return New(id, AssetKind.Score, name).Started(provenance!, nowUtc);
    }

    /// <summary>Registers a generated take (feature 009): ready at once with its stored file; long names are cut.</summary>
    /// <param name="id">New identifier.</param>
    /// <param name="provenance">Where it came from, with its generation.</param>
    /// <param name="stored">Name (cut to 120 characters), stored files and audio facts (48 kHz).</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    /// <returns>The ready item.</returns>
    public static Asset CreateGenerated(Guid id, Provenance provenance, GeneratedAudio stored, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(stored);
        Require(provenance, provenance?.Generation is not null, "A generated item needs its generation.");
        var asset = New(id, AssetKind.Audio, Text.TrimToLength(stored.Name, AssetNaming.MaxNameLength) ?? "Generated take").Started(provenance!, nowUtc);
        asset.MarkReady(stored.Files, nowUtc);
        asset.Audio = stored.Audio;
        return asset;
    }

    private static Asset New(Guid id, AssetKind kind, string name) => new() { Id = id, Kind = kind, Name = AssetNaming.Validate(name), Status = AssetStatus.Queued };

    private Asset Started(Provenance provenance, DateTime nowUtc)
    {
        (Provenance, TrackId, CreatedAt, UpdatedAt) = (provenance, provenance.TrackId, nowUtc, nowUtc);
        return this;
    }

    private static void Require(Provenance? provenance, bool recipeSet, string recipeMessage)
    {
        ArgumentNullException.ThrowIfNull(provenance);
        if (string.IsNullOrWhiteSpace(provenance.TrackTitle) || !recipeSet)
        {
            throw new ArgumentException($"The provenance needs a track title. {recipeMessage}", nameof(provenance));
        }
    }

    /// <summary>Renames the item.</summary>
    /// <param name="name">New name.</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    /// <exception cref="DomainValidationException">The name breaks a rule.</exception>
    public void Rename(string name, DateTime nowUtc) => (Name, UpdatedAt) = (AssetNaming.Validate(name), nowUtc);

    /// <summary>Records the worker render job.</summary>
    /// <param name="workerJobId">Job ID (32 lower-case hex characters).</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    public void AssignJob(string workerJobId, DateTime nowUtc)
    {
        var jobId = JobIds.Require(workerJobId, nameof(workerJobId));
        RequireActive();
        (WorkerJobId, UpdatedAt) = (jobId, nowUtc);
    }

    /// <summary>Applies the worker's progress (0..1): rendering, percent clamped, never lower; ignored when finished.</summary>
    /// <param name="fraction">Progress 0..1.</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    public void ApplyProgress(double fraction, DateTime nowUtc)
    {
        if (!IsActive)
        {
            return;
        }

        var percent = (int)Math.Round(Math.Clamp(fraction, 0, 1) * 100, MidpointRounding.AwayFromZero);
        (Status, Progress, UpdatedAt) = (AssetStatus.Rendering, Math.Max(Progress, percent), nowUtc);
    }

    /// <summary>Marks the item ready after its files were stored and verified.</summary>
    /// <param name="files">The stored files (at least one).</param>
    /// <param name="audio">Audio facts.</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    public void Complete(IReadOnlyList<AssetFile> files, AudioFacts audio, DateTime nowUtc)
    {
        MarkReady(files, nowUtc);
        Audio = audio;
    }

    /// <summary>Marks a score item ready after its files were stored and verified (feature 008).</summary>
    /// <param name="files">The stored files (at least one).</param>
    /// <param name="score">The score facts.</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    /// <exception cref="InvalidOperationException">The item is not a score.</exception>
    public void CompleteScore(IReadOnlyList<AssetFile> files, ScoreFacts score, DateTime nowUtc)
    {
        if (Kind != AssetKind.Score)
        {
            throw new InvalidOperationException($"Item {Id} is not a score.");
        }

        MarkReady(files, nowUtc);
        Score = score;
    }

    private void MarkReady(IReadOnlyList<AssetFile> files, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(files);
        if (files.Count == 0)
        {
            throw new ArgumentException("A ready item needs at least one file.", nameof(files));
        }

        RequireActive();
        (_files, SizeBytes) = (files.ToList(), files.Sum(file => file.SizeBytes));
        (Status, Progress, WorkerJobId, UpdatedAt) = (AssetStatus.Ready, 100, null, nowUtc);
    }

    /// <summary>Fails a queued or rendering item with a readable reason.</summary>
    /// <param name="error">Reason (blank becomes <see cref="RenderFailedDefault"/>, cut to 500 characters).</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    public void Fail(string error, DateTime nowUtc)
    {
        RequireActive();
        (Status, Error, WorkerJobId, UpdatedAt) = (AssetStatus.Failed, Text.TrimToLength(error, MaxErrorLength) ?? RenderFailedDefault, null, nowUtc);
    }

    /// <summary>Puts a failed item back to queued for a new render.</summary>
    /// <param name="nowUtc">Current time (UTC).</param>
    /// <exception cref="InvalidOperationException">The item is not failed.</exception>
    public void Retry(DateTime nowUtc)
    {
        if (Status != AssetStatus.Failed)
        {
            throw new InvalidOperationException("Only failed items can be retried.");
        }

        (Status, Progress, Error, WorkerJobId, UpdatedAt) = (AssetStatus.Queued, 0, null, null, nowUtc);
    }

    private void RequireActive()
    {
        if (!IsActive)
        {
            throw new InvalidOperationException($"Item {Id} is already {Status}.");
        }
    }
}
