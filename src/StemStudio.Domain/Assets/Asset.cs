using System.Text.RegularExpressions;

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
        RequireTitle(provenance);
        var asset = new Asset { Id = id, Kind = AssetKind.Audio, Name = AssetNaming.Validate(name), Status = AssetStatus.Queued };
        (asset.Provenance, asset.TrackId, asset.CreatedAt, asset.UpdatedAt) = (provenance, provenance.TrackId, nowUtc, nowUtc);
        return asset;
    }

    private static void RequireTitle(Provenance provenance)
    {
        ArgumentNullException.ThrowIfNull(provenance);
        if (string.IsNullOrWhiteSpace(provenance.TrackTitle))
        {
            throw new ArgumentException("The provenance needs a track title.", nameof(provenance));
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
        if (!JobIdPattern().IsMatch(workerJobId))
        {
            throw new ArgumentException($"'{workerJobId}' is not a worker job ID.", nameof(workerJobId));
        }

        RequireActive();
        (WorkerJobId, UpdatedAt) = (workerJobId, nowUtc);
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
        ArgumentNullException.ThrowIfNull(files);
        if (files.Count == 0)
        {
            throw new ArgumentException("A ready item needs at least one file.", nameof(files));
        }

        RequireActive();
        (_files, Audio, SizeBytes) = (files.ToList(), audio, files.Sum(file => file.SizeBytes));
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

    [GeneratedRegex(@"\A[0-9a-f]{32}\z", RegexOptions.CultureInvariant)]
    private static partial Regex JobIdPattern();
}
