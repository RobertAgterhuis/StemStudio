using System.Text.Json;
using System.Text.RegularExpressions;
using StemStudio.Agent.Ports;
using StemStudio.Contracts.Agent;

namespace StemStudio.Api.Agent.Loopback;

/// <summary>
/// Typed HTTP client of the API's own public endpoints (feature 011, FR-005): the server tools act through the same
/// validators as the UI and never touch the database. Problem responses become <see cref="AgentActionException"/> with a
/// readable message that is safe for the model (no URLs, paths or stack traces; server errors are generic).
/// </summary>
/// <param name="http">HTTP client with the loopback base address.</param>
public sealed partial class LoopbackClient(HttpClient http)
{
    /// <summary>Message of 5xx responses.</summary>
    public const string ServerErrorMessage = "StemStudio could not complete the action because of an internal error.";

    /// <summary>Message when the API cannot be reached.</summary>
    public const string UnreachableMessage = "StemStudio could not be reached.";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>The loopback base address.</summary>
    public Uri? BaseAddress => http.BaseAddress;

    /// <summary>GET with a JSON result.</summary>
    /// <typeparam name="T">Result type.</typeparam>
    /// <param name="path">Path and query.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The result.</returns>
    public Task<T> GetAsync<T>(string path, CancellationToken cancellationToken) => SendAsync<T>(HttpMethod.Get, path, null, cancellationToken);

    /// <summary>Sends a request with an optional JSON body and reads the JSON result.</summary>
    /// <typeparam name="T">Result type.</typeparam>
    /// <param name="method">HTTP method.</param>
    /// <param name="path">Path and query.</param>
    /// <param name="body">JSON body, or null for none.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The result.</returns>
    public async Task<T> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        using var response = await SendRawAsync(method, path, body, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken))!;
    }

    /// <summary>Sends a request without a result body.</summary>
    /// <param name="method">HTTP method.</param>
    /// <param name="path">Path and query.</param>
    /// <param name="body">JSON body, or null for none.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    public async Task SendAsync(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        using var response = await SendRawAsync(method, path, body, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendRawAsync(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));
        request.Content = body is null ? null : JsonContent.Create(body, body.GetType(), options: Json);
        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new AgentActionException(UnreachableMessage, exception);
        }

        if (!response.IsSuccessStatusCode)
        {
            using (response)
            {
                throw await ProblemAsync(response, cancellationToken);
            }
        }

        return response;
    }

    private static async Task<AgentActionException> ProblemAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var status = (int)response.StatusCode;
        if (status >= 500)
        {
            return new AgentActionException(status, null, ServerErrorMessage);
        }

        var problem = await ReadProblemAsync(response, cancellationToken);
        return new AgentActionException(status, CodeOf(problem.Type, status), Readable(problem, status));
    }

    private static async Task<LoopbackProblem> ReadProblemAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<LoopbackProblem>(Json, cancellationToken) ?? new LoopbackProblem();
        }
        catch (JsonException)
        {
            return new LoopbackProblem();
        }
    }

    private static string? CodeOf(string? type, int status) => type switch
    {
        { } uri when uri.StartsWith(AgentProblemTypes.BaseUri, StringComparison.Ordinal) => uri[AgentProblemTypes.BaseUri.Length..],
        { Length: > 0 } code when !code.Contains("://", StringComparison.Ordinal) => code,
        _ => status switch { 404 => "not-found", 409 => "conflict", 422 => "validation", _ => "invalid-request" },
    };

    private static string Readable(LoopbackProblem problem, int status)
    {
        var errors = problem.Errors?.SelectMany(pair => pair.Value.Select(message => $"{pair.Key}: {message}")) ?? [];
        var parts = new[] { problem.Detail ?? problem.Title }.Concat(errors).Where(part => !string.IsNullOrWhiteSpace(part));
        var text = Url().Replace(string.Join(" ", parts), "the address").Trim();
        text = text.Length == 0 ? $"StemStudio refused the action (HTTP {status})." : text;
        return text.Length <= AgentContractLimits.ErrorMaxLength ? text : text[..AgentContractLimits.ErrorMaxLength];
    }

    [GeneratedRegex(@"https?://\S+", RegexOptions.CultureInvariant)]
    private static partial Regex Url();

    private sealed record LoopbackProblem
    {
        public string? Type { get; init; }

        public string? Title { get; init; }

        public string? Detail { get; init; }

        public Dictionary<string, string[]>? Errors { get; init; }
    }
}
