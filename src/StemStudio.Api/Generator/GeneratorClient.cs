using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Polly.CircuitBreaker;
using Polly.Timeout;
using StemStudio.Api.Storage;

namespace StemStudio.Api.Generator;

/// <summary>Typed <see cref="HttpClient"/> for the generator service; resilience is added at registration.</summary>
/// <param name="http">Client whose base address is the generator URL (null when not installed).</param>
/// <param name="logger">Logger for generator problems (messages only).</param>
/// <param name="clock">Clock for the health timeout.</param>
public sealed partial class GeneratorClient(HttpClient http, ILogger<GeneratorClient> logger, TimeProvider clock) : IGeneratorClient
{
    /// <summary>Total time allowed for <c>GET /health</c> (backend <c>getGenerationStatus</c>: 2 s).</summary>
    public static readonly TimeSpan HealthTimeout = TimeSpan.FromSeconds(2);

    /// <summary>Serializer options of the generator wire format (camelCase members and enums).</summary>
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private const long MaxTakeBytes = 2L * 1024 * 1024 * 1024;

    /// <inheritdoc />
    public bool IsConfigured => http.BaseAddress is not null;

    /// <inheritdoc />
    public async Task<GeneratorHealth> GetHealthAsync(CancellationToken cancellationToken)
    {
        using var timeout = new CancellationTokenSource(HealthTimeout, clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            return (await ReadAsync<GeneratorHealth>(new HttpRequestMessage(HttpMethod.Get, "health"), false, linked.Token))!;
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new GeneratorUnavailableException("The generator service did not answer in time.", exception);
        }
    }

    /// <inheritdoc />
    public async Task<GeneratorJob> CreateRestyleAsync(GeneratorRestyleRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var content = new MultipartFormDataContent { { ParamsPart(new { kind = "restyle", restyle = request.Params }), "params" } };
        foreach (var stem in request.Stems)
        {
            content.Add(FilePart(stem.FilePath), "stem", stem.Name + ".flac");
        }

        return (await ReadAsync<GeneratorJob>(new HttpRequestMessage(HttpMethod.Post, "v1/generations") { Content = content }, false, cancellationToken))!;
    }

    /// <inheritdoc />
    public Task<GeneratorJob?> GetAsync(string generationId, CancellationToken cancellationToken) =>
        ReadAsync<GeneratorJob>(new HttpRequestMessage(HttpMethod.Get, JobPath(generationId)), true, cancellationToken);

    /// <inheritdoc />
    public Task<GeneratorJob?> CancelAsync(string generationId, CancellationToken cancellationToken) =>
        ReadAsync<GeneratorJob>(new HttpRequestMessage(HttpMethod.Post, JobPath(generationId) + "/cancel"), true, cancellationToken);

    /// <inheritdoc />
    public async Task DownloadTakeAsync(string generationId, int index, Stream destination, string sha256, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);
        var path = $"{JobPath(generationId)}/takes/{TakeIndex(index)}";
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        using var response = await ExchangeAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await ThrowOnFailureAsync(request, response, cancellationToken);
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        var copied = await HashingCopy.CopyAsync(source, destination, MaxTakeBytes, cancellationToken);
        if (!string.Equals(copied.Sha256, sha256, StringComparison.Ordinal))
        {
            throw new GeneratorIntegrityException($"Take {index} of generation {generationId} is damaged (SHA-256 mismatch).");
        }
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string generationId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, JobPath(generationId));
        using var response = await ExchangeAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken);
        if (response.StatusCode != HttpStatusCode.NotFound)
        {
            await ThrowOnFailureAsync(request, response, cancellationToken);
        }
    }

    private static StringContent ParamsPart(object body) =>
        new(JsonSerializer.Serialize(body, Json), Encoding.UTF8, "application/json");

    private static StreamContent FilePart(string path)
    {
        var part = new StreamContent(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true));
        part.Headers.ContentType = new MediaTypeHeaderValue("audio/flac");
        return part;
    }

    private async Task<T?> ReadAsync<T>(HttpRequestMessage request, bool notFoundIsNull, CancellationToken cancellationToken)
        where T : class
    {
        using (request)
        {
            using var response = await ExchangeAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken);
            if (notFoundIsNull && response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            await ThrowOnFailureAsync(request, response, cancellationToken);
            return await ParseAsync<T>(response, cancellationToken);
        }
    }

    private static async Task<T> ParseAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken) ?? throw new JsonException("The body is null.");
        }
        catch (JsonException exception)
        {
            throw new GeneratorUnavailableException("The generator service sent an invalid response.", exception);
        }
    }

    private async Task<HttpResponseMessage> ExchangeAsync(HttpRequestMessage request, HttpCompletionOption completion, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            throw new GeneratorNotInstalledException();
        }

        try
        {
            return await http.SendAsync(request, completion, cancellationToken);
        }
        catch (Exception exception) when (IsTransportFailure(exception, cancellationToken))
        {
            var unreachable = Describe(request);
            LogUnreachable(logger, unreachable, exception.Message);
            throw new GeneratorUnavailableException("The generator service cannot be reached.", exception);
        }
    }

    private static bool IsTransportFailure(Exception exception, CancellationToken cancellationToken) =>
        exception is HttpRequestException or TimeoutRejectedException or BrokenCircuitException
        || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested);

    private async Task ThrowOnFailureAsync(HttpRequestMessage request, HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var status = (int)response.StatusCode;
        if (status < 400)
        {
            return;
        }

        if (status >= 500)
        {
            var failed = Describe(request);
            LogUnreachable(logger, failed, "HTTP " + status.ToString(CultureInfo.InvariantCulture));
            throw new GeneratorUnavailableException($"The generator service answered {status}.");
        }

        var problem = await ProblemAsync(response, cancellationToken);
        var refused = Describe(request);
        LogRefused(logger, refused, status, problem.Detail);
        throw new GeneratorRequestException(response.StatusCode, problem.Code, problem.Detail);
    }

    private static async Task<(GeneratorProblemCode? Code, string Detail)> ProblemAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var fallback = response.ReasonPhrase ?? response.StatusCode.ToString();
        try
        {
            var body = await response.Content.ReadFromJsonAsync<ProblemBody>(Json, cancellationToken);
            return (CodeOf(body?.Code), body?.Detail ?? body?.Title ?? fallback);
        }
        catch (JsonException)
        {
            return (null, fallback);
        }
    }

    private static GeneratorProblemCode? CodeOf(string? code) =>
        code is not null && Enum.TryParse<GeneratorProblemCode>(code, true, out var parsed) && Enum.IsDefined(parsed) ? parsed : null;

    private static int TakeIndex(int index) => index is >= 1 and <= 2 ? index : throw new ArgumentException($"{index} is not a take number.", nameof(index));

    private static string JobPath(string generationId) =>
        JobIdPattern().IsMatch(generationId ?? string.Empty)
            ? $"v1/generations/{generationId}"
            : throw new ArgumentException($"'{generationId}' is not a generation id.", nameof(generationId));

    private static string Describe(HttpRequestMessage request) => $"{request.Method} {request.RequestUri}";

    [LoggerMessage(Level = LogLevel.Warning, Message = "Generator service unreachable on {Request}: {Reason}")]
    private static partial void LogUnreachable(ILogger logger, string request, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Generator service refused {Request} with {Status}: {Detail}")]
    private static partial void LogRefused(ILogger logger, string request, int status, string detail);

    [GeneratedRegex(@"\A[0-9a-f]{32}\z", RegexOptions.CultureInvariant)]
    private static partial Regex JobIdPattern();

    private sealed record ProblemBody(string? Title, string? Detail, string? Code);
}
