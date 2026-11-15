using StemStudio.Api.Worker;
using StemStudio.Domain.Assets;

namespace StemStudio.Api.Assets;

/// <summary>The state of a worker job of a library item (render or transcription; both use the render job states).</summary>
/// <param name="Status">State.</param>
/// <param name="Progress">Progress 0..1.</param>
/// <param name="Error">Failure text.</param>
public sealed record AssetJobState(WorkerRenderJobState Status, double Progress, string? Error)
{
    /// <summary>Applies the state to its item (not saved): a lost job and a failure fail it, progress moves it, completion runs <paramref name="complete"/>.</summary>
    /// <param name="asset">An active item.</param>
    /// <param name="state">The state, or null when the worker no longer knows the job.</param>
    /// <param name="complete">Stores the result of a completed job.</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    /// <returns>A task.</returns>
    public static Task ApplyAsync(Asset asset, AssetJobState? state, Func<Task> complete, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentNullException.ThrowIfNull(complete);
        if (state?.Status == WorkerRenderJobState.Completed)
        {
            return complete();
        }

        Apply(asset, state, nowUtc);
        return Task.CompletedTask;
    }

    private static void Apply(Asset asset, AssetJobState? state, DateTime nowUtc)
    {
        switch (state)
        {
            case null:
                asset.Fail(AssetJobApplier.LostJob, nowUtc);
                break;
            case { Status: WorkerRenderJobState.Running }:
                asset.ApplyProgress(state.Progress, nowUtc);
                break;
            case { Status: not WorkerRenderJobState.Queued }:
                asset.Fail(state.Error ?? string.Empty, nowUtc);
                break;
        }
    }

    /// <summary>The failure handler of <see cref="StoreAsync"/>: deletes the item's stored files and fails it.</summary>
    /// <param name="asset">The item.</param>
    /// <param name="files">Asset file store.</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    /// <returns>The handler.</returns>
    public static Action<string> Discard(Asset asset, IAssetFileStore files, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentNullException.ThrowIfNull(files);
        return reason =>
        {
            files.DeleteAll(asset.Id);
            asset.Fail(reason, nowUtc);
        };
    }

    /// <summary>Stores a completed result; a readable failure is handed to <paramref name="onFailure"/> (other exceptions propagate).</summary>
    /// <param name="store">Copies and verifies the files and completes the item.</param>
    /// <param name="failure">The readable reason of an exception, or null to rethrow it.</param>
    /// <param name="onFailure">Deletes the stored files and fails the item.</param>
    /// <returns>A task.</returns>
    public static async Task StoreAsync(Func<Task> store, Func<Exception, string?> failure, Action<string> onFailure)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(failure);
        ArgumentNullException.ThrowIfNull(onFailure);
        try
        {
            await store();
        }
        catch (Exception exception) when (failure(exception) is { } reason)
        {
            onFailure(reason);
        }
    }
}
