using System.Net;
using StemStudio.Domain.Tracks.Detail;

namespace StemStudio.Api.Worker;

/// <summary>Detail job calls of the worker API 1.1.0 (<c>/v1/detail-jobs/...</c>).</summary>
public sealed partial class WorkerClient
{
    /// <inheritdoc />
    public async Task<WorkerDetailJob> CreateDetailJobAsync(string standardJobId, CancellationToken cancellationToken)
    {
        var body = new CreateDetailJobBody(ValidJobId(standardJobId));
        using var message = new HttpRequestMessage(HttpMethod.Post, "v1/detail-jobs") { Content = JsonContent.Create(body, options: Json) };
        return await SendForAsync<WorkerDetailJob>(message, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<WorkerDetailJob?> GetDetailJobAsync(string detailJobId, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, DetailJobPath(detailJobId));
        using var response = await SendAsync(message, HttpCompletionOption.ResponseContentRead, cancellationToken);
        return response.StatusCode == HttpStatusCode.NotFound ? null : await ReadAsync<WorkerDetailJob>(message, response, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<WorkerDetailJob> CancelDetailJobAsync(string detailJobId, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, $"{DetailJobPath(detailJobId)}/cancel");
        return await SendForAsync<WorkerDetailJob>(message, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<WorkerDetailResult> GetDetailResultAsync(string detailJobId, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, $"{DetailJobPath(detailJobId)}/result");
        return await SendForAsync<WorkerDetailResult>(message, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DownloadDetailStemAsync(WorkerDetailStemRef stem, Stream destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stem);
        if (!DetailCatalog.IsAllowed(stem.Parent, stem.Name))
        {
            throw new ArgumentException($"'{stem.Parent}/{stem.Name}' is not a detail stem.", nameof(stem));
        }

        using var message = new HttpRequestMessage(HttpMethod.Get, $"{DetailJobPath(stem.DetailJobId)}/stems/{stem.Parent}/{stem.Name}");
        using var response = await SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await EnsureSuccessAsync(message, response, cancellationToken);
        await response.Content.CopyToAsync(destination, cancellationToken);
    }

    private static string DetailJobPath(string detailJobId) => $"v1/detail-jobs/{ValidJobId(detailJobId)}";

    private static string ValidJobId(string jobId) => JobIdPattern().IsMatch(jobId)
        ? jobId
        : throw new ArgumentException($"'{jobId}' is not a worker job ID.", nameof(jobId));

    private sealed record CreateDetailJobBody(string StandardJobId);
}
