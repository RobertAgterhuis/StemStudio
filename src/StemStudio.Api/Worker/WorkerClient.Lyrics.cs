using System.Net;

namespace StemStudio.Api.Worker;

/// <summary>Lyrics job calls of the worker API 1.5.0 (feature 006). Bodies carry copyrighted text and are never logged.</summary>
public sealed partial class WorkerClient
{
    /// <inheritdoc />
    public Task<WorkerLyricsJob> CreateLyricsLookupJobAsync(WorkerLyricsLookupRequest request, CancellationToken cancellationToken) =>
        CreateLyricsJobAsync(request, cancellationToken);

    /// <inheritdoc />
    public Task<WorkerLyricsJob> CreateLyricsAlignJobAsync(WorkerLyricsAlignRequest request, CancellationToken cancellationToken) =>
        CreateLyricsJobAsync(request, cancellationToken);

    /// <inheritdoc />
    public async Task<WorkerLyricsJob?> GetLyricsJobAsync(string lyricsJobId, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, LyricsJobPath(lyricsJobId));
        using var response = await SendAsync(message, HttpCompletionOption.ResponseContentRead, cancellationToken);
        return response.StatusCode == HttpStatusCode.NotFound ? null : await ReadAsync<WorkerLyricsJob>(message, response, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<WorkerLyricsResult> GetLyricsResultAsync(string lyricsJobId, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, $"{LyricsJobPath(lyricsJobId)}/result");
        return await SendForAsync<WorkerLyricsResult>(message, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<WorkerLyricsJob> CancelLyricsJobAsync(string lyricsJobId, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, $"{LyricsJobPath(lyricsJobId)}/cancel");
        return await SendForAsync<WorkerLyricsJob>(message, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteLyricsJobAsync(string lyricsJobId, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Delete, LyricsJobPath(lyricsJobId));
        using var response = await SendAsync(message, HttpCompletionOption.ResponseContentRead, cancellationToken);
        if (response.StatusCode != HttpStatusCode.NotFound)
        {
            await EnsureSuccessAsync(message, response, cancellationToken);
        }
    }

    private async Task<WorkerLyricsJob> CreateLyricsJobAsync<T>(T request, CancellationToken cancellationToken)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(request);
        using var message = new HttpRequestMessage(HttpMethod.Post, "v1/lyrics-jobs") { Content = JsonContent.Create(request, options: Json) };
        return await SendForAsync<WorkerLyricsJob>(message, cancellationToken);
    }

    private static string LyricsJobPath(string lyricsJobId) => $"v1/lyrics-jobs/{ValidJobId(lyricsJobId)}";
}
