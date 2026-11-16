using System.Text.Json;
using System.Text.Json.Serialization;

namespace StemStudio.Web.Api;

/// <summary>Maps an error response body (RFC 9457 problem details) to an <see cref="ApiError"/>.</summary>
internal static class ProblemReader
{
    /// <summary>Reads the problem body; falls back to a status-only error when it is missing or invalid.</summary>
    /// <param name="response">A non-success response.</param>
    /// <param name="ct">Cancels reading.</param>
    /// <returns>The mapped error.</returns>
    public static async Task<ApiError> ReadAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var problem = await TryReadAsync(response, ct);
        if (problem?.Title is null)
        {
            return ApiError.FromStatus(response.StatusCode);
        }

        return new ApiError(response.StatusCode, problem.Title, problem.Detail, problem.Errors ?? [])
        {
            Type = problem.Type,
            Extensions = problem.Extensions ?? [],
        };
    }

    private static async Task<ProblemBody?> TryReadAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.Content.Headers.ContentType?.MediaType?.EndsWith("json", StringComparison.OrdinalIgnoreCase) != true)
        {
            return null;
        }

        try
        {
            return await response.Content.ReadFromJsonAsync<ProblemBody>(StemStudioApiClient.Json, ct);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed class ProblemBody
    {
        public string? Type { get; init; }

        public string? Title { get; init; }

        public string? Detail { get; init; }

        public Dictionary<string, string[]>? Errors { get; init; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Extensions { get; init; }
    }
}
