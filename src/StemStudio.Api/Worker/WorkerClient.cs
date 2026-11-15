using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Polly.CircuitBreaker;
using Polly.Timeout;
using StemStudio.Domain.Tracks;

namespace StemStudio.Api.Worker;

/// <summary>Typed <see cref="HttpClient"/> for the worker API; resilience is added at registration.</summary>
/// <param name="http">Client whose base address is the worker URL (ending with a slash).</param>
/// <param name="logger">Logger for worker problems (messages only, no stack traces).</param>
public sealed partial class WorkerClient(HttpClient http, ILogger<WorkerClient> logger) : IWorkerClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    /// <inheritdoc />
    public async Task<WorkerJob> CreateUrlJobAsync(WorkerUrlJobRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var body = new CreateJobBody(UrlSourceType(request.SourceType), request.Url);
        using var message = new HttpRequestMessage(HttpMethod.Post, "v1/jobs") { Content = JsonContent.Create(body, options: Json) };
        return await SendForAsync<WorkerJob>(message, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<WorkerJob> CreateUploadJobAsync(WorkerUpload upload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(upload);
        await using var file = new FileStream(upload.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        var part = new StreamContent(file);
        part.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        using var content = new MultipartFormDataContent { { part, "file", upload.FileName } };
        using var message = new HttpRequestMessage(HttpMethod.Post, "v1/jobs") { Content = content };
        return await SendForAsync<WorkerJob>(message, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<WorkerJob?> GetJobAsync(string jobId, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, JobPath(jobId));
        using var response = await SendAsync(message, HttpCompletionOption.ResponseContentRead, cancellationToken);
        return response.StatusCode == HttpStatusCode.NotFound ? null : await ReadAsync<WorkerJob>(message, response, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<WorkerJobResult> GetResultAsync(string jobId, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, $"{JobPath(jobId)}/result");
        return await SendForAsync<WorkerJobResult>(message, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DownloadStemAsync(string jobId, string stem, Stream destination, CancellationToken cancellationToken)
    {
        if (!StemCatalog.IsKnown(stem))
        {
            throw new ArgumentException($"'{stem}' is not a stem name.", nameof(stem));
        }

        await DownloadAsync($"{JobPath(jobId)}/stems/{stem}", destination, cancellationToken);
    }

    /// <summary>Streams a file response into the destination (headers read first, then copied).</summary>
    private async Task DownloadAsync(string path, Stream destination, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, path);
        using var response = await SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await EnsureSuccessAsync(message, response, cancellationToken);
        await response.Content.CopyToAsync(destination, cancellationToken);
    }

    private static string UrlSourceType(SourceType type) => type switch
    {
        SourceType.Youtube => "youtube",
        SourceType.Spotify => "spotify",
        _ => throw new ArgumentException("Uploads are sent with CreateUploadJobAsync.", nameof(type)),
    };

    private static string JobPath(string jobId) => $"v1/jobs/{ValidJobId(jobId)}";

    private async Task<T> SendForAsync<T>(HttpRequestMessage message, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(message, HttpCompletionOption.ResponseContentRead, cancellationToken);
        return await ReadAsync<T>(message, response, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage message, HttpCompletionOption completion, CancellationToken cancellationToken)
    {
        try
        {
            return await http.SendAsync(message, completion, cancellationToken);
        }
        catch (Exception exception) when (IsTransportFailure(exception, cancellationToken))
        {
            var unreachable = Describe(message);
            LogUnavailable(logger, unreachable, exception.Message);
            throw new WorkerUnavailableException("The processing service cannot be reached.", exception);
        }
    }

    private static bool IsTransportFailure(Exception exception, CancellationToken cancellationToken) =>
        exception is HttpRequestException or TimeoutRejectedException or BrokenCircuitException
        || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested);

    private async Task<T> ReadAsync<T>(HttpRequestMessage message, HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await EnsureSuccessAsync(message, response, cancellationToken);
        try
        {
            return await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken)
                ?? throw new JsonException("Empty response.");
        }
        catch (JsonException exception)
        {
            throw new WorkerUnavailableException("The processing service sent an invalid response.", exception);
        }
    }

    private async Task EnsureSuccessAsync(HttpRequestMessage message, HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var status = (int)response.StatusCode;
        throw status >= 500 ? ServerFailure(message, status) : await RejectionAsync(message, response, cancellationToken);
    }

    private async Task<WorkerRequestException> RejectionAsync(HttpRequestMessage message, HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var detail = await ReadDetailAsync(response, cancellationToken);
        var rejected = Describe(message);
        LogRejected(logger, rejected, (int)response.StatusCode, detail);
        return new WorkerRequestException(response.StatusCode, detail);
    }

    private WorkerUnavailableException ServerFailure(HttpRequestMessage message, int status)
    {
        var failed = Describe(message);
        LogUnavailable(logger, failed, "HTTP " + status.ToString(CultureInfo.InvariantCulture));
        return new WorkerUnavailableException($"The processing service answered {status}.");
    }

    private static string Describe(HttpRequestMessage message) => $"{message.Method} {message.RequestUri}";

    private static async Task<string> ReadDetailAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var fallback = response.ReasonPhrase ?? response.StatusCode.ToString();
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<WorkerProblem>(Json, cancellationToken);
            return DetailOf(problem, fallback);
        }
        catch (JsonException)
        {
            return fallback;
        }
    }

    private static string DetailOf(WorkerProblem? problem, string fallback) => problem?.Detail ?? problem?.Title ?? fallback;

    [LoggerMessage(Level = LogLevel.Warning, Message = "Processing service unreachable on {Request}: {Reason}")]
    private static partial void LogUnavailable(ILogger logger, string request, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Processing service rejected {Request} with {Status}: {Detail}")]
    private static partial void LogRejected(ILogger logger, string request, int status, string detail);

    [GeneratedRegex(@"\A[0-9a-f]{32}\z", RegexOptions.CultureInvariant)]
    private static partial Regex JobIdPattern();

    private sealed record CreateJobBody(string SourceType, string Url);

    private sealed record WorkerProblem(string? Title, string? Detail);
}
