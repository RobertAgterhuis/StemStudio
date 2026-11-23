using StemStudio.Api.Assets;
using StemStudio.Api.Worker;
using StemStudio.Domain.Assets;

namespace StemStudio.Api.Handouts;

/// <summary>
/// Applies a worker handout job to its item (data-model 5.4, asset sync <c>WorkerJobKind.Handout</c>): progress, failure, or
/// completion. The PDF is copied to <c>assets/&lt;id&gt;/handout.pdf</c>, its SHA-256 compared with the worker's, the result
/// checked, and only then is the item ready with pages, outline and warnings.
/// </summary>
/// <param name="worker">The worker insights lane.</param>
/// <param name="files">Asset file store.</param>
/// <param name="clock">Clock.</param>
public sealed class HandoutJobApplier(IWorkerInsightsClient worker, IAssetFileStore files, TimeProvider clock)
{
    /// <summary>Error when the copied PDF does not match the worker's hash.</summary>
    public const string DamagedFile = "The handout PDF was damaged while it was copied; try again.";

    /// <summary>Error when the worker's result breaks the contract.</summary>
    public const string InvalidResult = "The processing service sent an unusable handout result; try again.";

    /// <summary>Role of the PDF.</summary>
    public const string PdfRole = "pdf";

    private const int MaxOutline = 1000;
    private const int MaxWarnings = 50;
    private const int MaxText = 300;
    private const int MaxTitle = 200;

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    /// <summary>Applies the job state (not saved).</summary>
    /// <param name="asset">An active handout item.</param>
    /// <param name="job">The job, or null when the worker no longer knows it.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    public Task ApplyAsync(Asset asset, WorkerHandoutJob? job, CancellationToken cancellationToken) =>
        AssetJobState.ApplyAsync(asset, job is null ? null : new AssetJobState(job.Status, job.Progress, job.Error), () => CompleteAsync(asset, job!, cancellationToken), Now);

    private Task CompleteAsync(Asset asset, WorkerHandoutJob job, CancellationToken cancellationToken) => AssetJobState.StoreAsync(
        () => StoreAsync(asset, job, cancellationToken),
        exception => AssetJobApplier.FailureReason(exception)?.Replace("rendered file", "handout PDF", StringComparison.Ordinal),
        AssetJobState.Discard(asset, files, Now));

    private async Task StoreAsync(Asset asset, WorkerHandoutJob job, CancellationToken cancellationToken)
    {
        var result = job.Result is { } completed && IsUsable(completed) ? completed : throw new InvalidDataException(InvalidResult);
        var stored = await files.SaveAsync(asset.Id, Asset.HandoutFileName, (stream, ct) => worker.DownloadHandoutFileAsync(job.HandoutJobId, stream, ct), cancellationToken);
        if (!string.Equals(stored.Sha256, result.Sha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException(DamagedFile);
        }

        var warnings = result.Warnings.Select(warning => warning with { Text = Cut(warning.Text) }).ToList();
        asset.CompleteHandout([new AssetFile(PdfRole, Asset.HandoutFileName, "application/pdf", stored.SizeBytes, stored.Sha256)], new HandoutRendering(result.Pages, result.Outline.Select(title => title.Length <= MaxTitle ? title : title[..MaxTitle]).ToList(), warnings), Now);
    }

    private static bool IsUsable(WorkerHandoutResult result) =>
        result is { Pages: >= 1, Outline.Count: <= MaxOutline, Warnings.Count: <= MaxWarnings }
        && result.ContentType == "application/pdf"
        && result.Outline.All(title => title is not null)
        && result.Warnings.All(warning => warning is not null && Enum.IsDefined(warning.Code) && warning.Text is not null);

    private static string Cut(string text) => text.Length <= MaxText ? text : text[..MaxText];
}

/// <summary>The worker client and applier of handout items, for the asset synchroniser.</summary>
/// <param name="Worker">The worker insights lane.</param>
/// <param name="Applier">Applies handout jobs.</param>
public sealed record HandoutJobs(IWorkerInsightsClient Worker, HandoutJobApplier Applier);
