using System.Net;

namespace StemStudio.Api.Worker;

/// <summary>Render job calls of the worker API 1.4.0 (feature 007).</summary>
public sealed partial class WorkerClient
{
    /// <inheritdoc />
    public async Task<WorkerRenderJob> CreateRenderJobAsync(WorkerRenderJobRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var message = new HttpRequestMessage(HttpMethod.Post, "v1/render-jobs") { Content = JsonContent.Create(request, options: Json) };
        return await SendForAsync<WorkerRenderJob>(message, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<WorkerRenderJob?> GetRenderJobAsync(string renderJobId, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, RenderJobPath(renderJobId));
        using var response = await SendAsync(message, HttpCompletionOption.ResponseContentRead, cancellationToken);
        return response.StatusCode == HttpStatusCode.NotFound ? null : await ReadAsync<WorkerRenderJob>(message, response, cancellationToken);
    }

    /// <inheritdoc />
    public Task DownloadRenderFileAsync(string renderJobId, Stream destination, CancellationToken cancellationToken) =>
        DownloadAsync($"{RenderJobPath(renderJobId)}/file", destination, cancellationToken);

    /// <inheritdoc />
    public Task DeleteRenderJobAsync(string renderJobId, CancellationToken cancellationToken) =>
        DeleteToleratingMissingAsync(RenderJobPath(renderJobId), cancellationToken);

    /// <summary>Sends a <c>DELETE</c>; 404 (already gone) counts as success.</summary>
    private async Task DeleteToleratingMissingAsync(string path, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Delete, path);
        using var response = await SendAsync(message, HttpCompletionOption.ResponseContentRead, cancellationToken);
        if (response.StatusCode != HttpStatusCode.NotFound)
        {
            await EnsureSuccessAsync(message, response, cancellationToken);
        }
    }

    private static string RenderJobPath(string renderJobId) => $"v1/render-jobs/{ValidJobId(renderJobId)}";
}
