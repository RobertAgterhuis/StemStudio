using StemStudio.Api.Assets;
using StemStudio.Api.Sync;
using StemStudio.Api.Worker;
using StemStudio.Domain;
using StemStudio.Domain.Assets;
using StemStudio.Domain.Scores;

namespace StemStudio.Api.Scores;

/// <summary>
/// Applies a worker transcription job to its score item (data-model 2.9, 4.1): progress, failure, or completion. The
/// three files are copied to <c>assets/&lt;id&gt;/score.{pdf,musicxml,mid}</c>, each SHA-256 compared with the worker's,
/// the summary validated, and only then is the item ready.
/// </summary>
/// <param name="worker">Worker client.</param>
/// <param name="files">Asset file store.</param>
/// <param name="devices">Validates the device.</param>
/// <param name="clock">Clock.</param>
/// <param name="hooks">Run after the item became ready (feature 014 score views); a failing hook is logged only.</param>
/// <param name="logger">Logger.</param>
public sealed partial class ScoreJobApplier(
    IWorkerClient worker, IAssetFileStore files, JobDeviceApplier devices, TimeProvider clock, IEnumerable<IAssetReadyHook> hooks, ILogger<ScoreJobApplier> logger)
{
    /// <summary>Error when a copied file does not match the worker's hash.</summary>
    public const string DamagedFile = "A score file was damaged while it was copied; try again.";

    private static readonly Dictionary<string, string> StoredNames = new(StringComparer.Ordinal)
    {
        ["pdf"] = "score.pdf",
        ["musicxml"] = "score.musicxml",
        ["midi"] = "score.mid",
    };

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    /// <summary>Applies the transcription job state to the score item (not saved).</summary>
    /// <param name="asset">An active score item.</param>
    /// <param name="job">The transcription job, or null when the worker no longer knows it.</param>
    /// <param name="cancellationToken">Cancellation of the file copies.</param>
    /// <returns>A task that completes when the state is applied.</returns>
    /// <exception cref="WorkerUnavailableException">The worker became unreachable while the files were copied.</exception>
    public Task ApplyAsync(Asset asset, WorkerTranscriptionJob? job, CancellationToken cancellationToken) =>
        AssetJobState.ApplyAsync(asset, job is null ? null : new(job.Status, job.Progress, job.Error), () => CompleteAsync(asset, job!, cancellationToken), Now);

    private Task CompleteAsync(Asset asset, WorkerTranscriptionJob job, CancellationToken cancellationToken) => AssetJobState.StoreAsync(
        () => CompleteScoreAsync(asset, job, cancellationToken),
        FailureReason,
        AssetJobState.Discard(asset, files, Now));

    private async Task CompleteScoreAsync(Asset asset, WorkerTranscriptionJob job, CancellationToken cancellationToken)
    {
        var result = job.Result ?? throw new InvalidDataException("The completed job has no result.");
        var facts = Facts(asset, result.Summary, job.Device);
        asset.CompleteScore(await StoreAsync(asset, job.TranscriptionJobId, result.Files, cancellationToken), facts, Now);
        await RunReadyHooksAsync(asset, cancellationToken);
    }

    private async Task RunReadyHooksAsync(Asset asset, CancellationToken cancellationToken)
    {
        foreach (var hook in hooks)
        {
            try
            {
                await hook.OnReadyAsync(asset, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogHookFailed(logger, asset.Id, exception);
            }
        }
    }

    /// <summary>The validated facts: the summary, the device, and the piano model credit when the worker gave none (CC BY 4.0).</summary>
    private ScoreFacts Facts(Asset asset, WorkerScoreSummary summary, WorkerJobDevice? device)
    {
        var facts = ScoreMapper.ToFacts(summary) with
        {
            Attribution = summary.Attribution ?? (summary.Engine == ScoreEngine.PianoCrnn ? ProfileCatalog.PianoAttribution : null),
            Device = devices.Convert(asset.Id, device),
        };
        ScoreFactsRules.Validate(facts);
        return facts;
    }

    private async Task<List<AssetFile>> StoreAsync(Asset asset, string jobId, IReadOnlyList<WorkerTranscriptionFile> results, CancellationToken cancellationToken)
    {
        var stored = new List<AssetFile>();
        foreach (var result in results)
        {
            var fileName = StoredNames.TryGetValue(result.Role, out var name) ? name : throw new ArgumentException($"Unknown file role {result.Role}.", nameof(results));
            var saved = await files.SaveAsync(asset.Id, fileName, (stream, ct) => worker.DownloadTranscriptionFileAsync(jobId, result.Role, stream, ct), cancellationToken);
            stored.Add(saved.Sha256 == result.Sha256 ? new AssetFile(result.Role, fileName, result.ContentType, saved.SizeBytes, saved.Sha256) : throw new InvalidDataException(DamagedFile));
        }

        return stored;
    }

    /// <summary>A readable reason, or null for an unreachable worker (kept for the next poll) and unexpected errors.</summary>
    private static string? FailureReason(Exception exception) => exception is DomainValidationException invalid
        ? $"The score facts are invalid: {invalid.Message}"
        : AssetJobApplier.FailureReason(exception)?.Replace("rendered file", "score file", StringComparison.Ordinal);

    [LoggerMessage(Level = LogLevel.Error, Message = "Score {AssetId}: a ready hook failed; the score stays ready")]
    private static partial void LogHookFailed(ILogger logger, Guid assetId, Exception exception);
}
