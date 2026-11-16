using System.Text.Json;

namespace StemStudio.Web.Api;

/// <summary>
/// Shared HTTP plumbing of the typed backend clients: sends a request, maps a transport failure to
/// <see cref="ApiError.Unreachable"/> and a non-success response to an <see cref="ApiException"/> with the problem details.
/// </summary>
/// <param name="http">The configured HTTP client.</param>
/// <param name="json">The JSON options of the contract.</param>
internal sealed class ApiTransport(HttpClient http, JsonSerializerOptions json)
{
    /// <summary>Sends a request and reads the JSON body.</summary>
    /// <typeparam name="T">The response type.</typeparam>
    /// <param name="method">The HTTP method.</param>
    /// <param name="uri">The relative URI.</param>
    /// <param name="content">The body, if any.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The deserialised body.</returns>
    public async Task<T> SendAsync<T>(HttpMethod method, string uri, HttpContent? content, CancellationToken ct)
    {
        using var response = await SendRawAsync(new HttpRequestMessage(method, new Uri(uri, UriKind.Relative)) { Content = content }, ct);
        var value = await response.Content.ReadFromJsonAsync<T>(json, ct);
        return value ?? throw new ApiException(ApiError.FromStatus(response.StatusCode) with { Title = "The server returned an empty response." });
    }

    /// <summary>Sends a request whose response body is ignored.</summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="uri">The relative URI.</param>
    /// <param name="content">The body, if any.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>A task that completes when the call succeeded.</returns>
    public async Task SendAsync(HttpMethod method, string uri, HttpContent? content, CancellationToken ct)
    {
        using var response = await SendRawAsync(new HttpRequestMessage(method, new Uri(uri, UriKind.Relative)) { Content = content }, ct);
    }

    /// <summary>Sends a prepared request (disposed here) and returns the successful response.</summary>
    /// <param name="request">The request.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <param name="completion">When the call completes (headers only for streams).</param>
    /// <returns>The successful response; the caller disposes it.</returns>
    public async Task<HttpResponseMessage> SendRawAsync(HttpRequestMessage request, CancellationToken ct, HttpCompletionOption completion = HttpCompletionOption.ResponseContentRead)
    {
        using (request)
        {
            var response = await TransmitAsync(request, completion, ct);
            if (response.IsSuccessStatusCode)
            {
                return response;
            }

            using (response)
            {
                throw new ApiException(await ProblemReader.ReadAsync(response, ct));
            }
        }
    }

    private async Task<HttpResponseMessage> TransmitAsync(HttpRequestMessage request, HttpCompletionOption completion, CancellationToken ct)
    {
        try
        {
            return await http.SendAsync(request, completion, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new ApiException(ApiError.Unreachable, ex);
        }
    }
}
