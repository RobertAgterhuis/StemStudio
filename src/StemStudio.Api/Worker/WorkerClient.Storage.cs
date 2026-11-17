using System.Globalization;

namespace StemStudio.Api.Worker;

/// <summary>Storage, clean-up and rehydration calls of the worker API 1.7.0 (feature 013).</summary>
public sealed partial class WorkerClient
{
    /// <inheritdoc />
    public async Task<WorkerStorage> GetStorageAsync(CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, "v1/storage");
        return await SendWithinAsync<WorkerStorage>(message, WorkerTimeouts.Storage, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<WorkerStorageCleanupResult> CleanStorageAsync(WorkerStorageCleanupRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var message = new HttpRequestMessage(HttpMethod.Post, "v1/storage/cleanup") { Content = JsonContent.Create(request, options: Json) };
        return await SendForAsync<WorkerStorageCleanupResult>(message, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<WorkerRehydratedJob> RehydrateJobAsync(WorkerRehydrateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var body = new RehydrateBody(request.TrackId.ToString("D", CultureInfo.InvariantCulture), request.SourceType, request.SourceKey, request.Model, request.Stems);
        using var message = new HttpRequestMessage(HttpMethod.Post, "v1/jobs/rehydrate") { Content = JsonContent.Create(body, options: Json) };
        return await SendForAsync<WorkerRehydratedJob>(message, cancellationToken);
    }

    /// <summary>Sends and reads within a total time; running out of time is "unavailable".</summary>
    private async Task<T> SendWithinAsync<T>(HttpRequestMessage message, TimeSpan within, CancellationToken cancellationToken)
    {
        using var timeout = new CancellationTokenSource(within, clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            return await SendForAsync<T>(message, linked.Token);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new WorkerUnavailableException("The processing service did not answer in time.", exception);
        }
    }

    private sealed record RehydrateBody(string TrackId, string SourceType, string SourceKey, string Model, IReadOnlyList<string> Stems);
}
