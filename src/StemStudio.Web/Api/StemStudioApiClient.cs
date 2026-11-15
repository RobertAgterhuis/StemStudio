using System.Net.Http.Headers;
using System.Text.Json;
using StemStudio.Contracts;
using StemStudio.Contracts.Health;
using StemStudio.Contracts.Importing;
using StemStudio.Contracts.Mixes;
using StemStudio.Contracts.Tracks;

namespace StemStudio.Web.Api;

/// <summary>
/// <see cref="HttpClient"/>-based implementation of <see cref="IStemStudioApiClient"/>, registered as a
/// typed client on <see cref="StemStudioWebOptions.ApiClientName"/> with the server-side API base URL.
/// </summary>
/// <param name="http">The configured HTTP client.</param>
public sealed class StemStudioApiClient(HttpClient http) : IStemStudioApiClient
{
    /// <summary>JSON options of the backend contract (camelCase; enums carry their own converters).</summary>
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public Task<TrackDto> ImportUrlAsync(string url, CancellationToken cancellationToken = default) =>
        SendAsync<TrackDto>(HttpMethod.Post, ApiRoutes.Imports, JsonBody(new ImportUrlRequest(url)), cancellationToken);

    /// <inheritdoc />
    public Task<TrackDto> ImportUploadAsync(UploadFile file, CancellationToken cancellationToken = default) =>
        SendAsync<TrackDto>(HttpMethod.Post, ApiRoutes.ImportUpload, MultipartBody(file), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<TrackDto>> ListTracksAsync(CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<TrackDto>>(HttpMethod.Get, ApiRoutes.Tracks, null, cancellationToken);

    /// <inheritdoc />
    public Task<TrackDto> GetTrackAsync(Guid trackId, CancellationToken cancellationToken = default) =>
        SendAsync<TrackDto>(HttpMethod.Get, ApiRoutes.Track(trackId), null, cancellationToken);

    /// <inheritdoc />
    public Task<TrackDto> RetryTrackAsync(Guid trackId, CancellationToken cancellationToken = default) =>
        SendAsync<TrackDto>(HttpMethod.Post, ApiRoutes.RetryTrack(trackId), null, cancellationToken);

    /// <inheritdoc />
    public async Task DeleteTrackAsync(Guid trackId, CancellationToken cancellationToken = default)
    {
        using var response = await SendRawAsync(HttpMethod.Delete, ApiRoutes.Track(trackId), null, cancellationToken);
    }

    /// <inheritdoc />
    public Task<TrackDto> StartDetailAsync(Guid trackId, CancellationToken cancellationToken = default) =>
        SendAsync<TrackDto>(HttpMethod.Post, ApiRoutes.StartDetail(trackId), null, cancellationToken);

    /// <inheritdoc />
    public Task<TrackDto> CancelDetailAsync(Guid trackId, CancellationToken cancellationToken = default) =>
        SendAsync<TrackDto>(HttpMethod.Post, ApiRoutes.CancelDetail(trackId), null, cancellationToken);

    /// <inheritdoc />
    public Task<ProcessingDeviceDto> GetDeviceAsync(CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    /// <inheritdoc />
    public Task<IReadOnlyList<MixSummaryDto>> ListMixesAsync(Guid? trackId = null, CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<MixSummaryDto>>(HttpMethod.Get, MixesQuery(trackId), null, cancellationToken);

    /// <inheritdoc />
    public Task<MixDto> GetMixAsync(Guid mixId, CancellationToken cancellationToken = default) =>
        SendAsync<MixDto>(HttpMethod.Get, ApiRoutes.Mix(mixId), null, cancellationToken);

    /// <inheritdoc />
    public Task<MixDto> CreateMixAsync(CreateMixRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<MixDto>(HttpMethod.Post, ApiRoutes.Mixes, JsonBody(request), cancellationToken);

    /// <inheritdoc />
    public Task<MixDto> UpdateMixAsync(Guid mixId, MixSettingsDto settings, CancellationToken cancellationToken = default) =>
        SendAsync<MixDto>(HttpMethod.Put, ApiRoutes.Mix(mixId), JsonBody(settings), cancellationToken);

    /// <inheritdoc />
    public async Task DeleteMixAsync(Guid mixId, CancellationToken cancellationToken = default)
    {
        using var response = await SendRawAsync(HttpMethod.Delete, ApiRoutes.Mix(mixId), null, cancellationToken);
    }

    /// <inheritdoc />
    public Task<MixDto> DuplicateMixAsync(Guid mixId, CancellationToken cancellationToken = default) =>
        SendAsync<MixDto>(HttpMethod.Post, ApiRoutes.DuplicateMix(mixId), null, cancellationToken);

    private static string MixesQuery(Guid? trackId) =>
        trackId is { } id ? $"{ApiRoutes.Mixes}?trackId={id}" : ApiRoutes.Mixes;

    private static JsonContent JsonBody<T>(T value) => JsonContent.Create(value, options: Json);

    private static MultipartFormDataContent MultipartBody(UploadFile file)
    {
        var stream = new StreamContent(file.Content);
        stream.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
        return new MultipartFormDataContent { { stream, "file", file.FileName } };
    }

    private async Task<T> SendAsync<T>(HttpMethod method, string uri, HttpContent? content, CancellationToken ct)
    {
        using var response = await SendRawAsync(method, uri, content, ct);
        var value = await response.Content.ReadFromJsonAsync<T>(Json, ct);
        return value ?? throw new ApiException(ApiError.FromStatus(response.StatusCode) with { Title = "The server returned an empty response." });
    }

    private async Task<HttpResponseMessage> SendRawAsync(HttpMethod method, string uri, HttpContent? content, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, new Uri(uri, UriKind.Relative)) { Content = content };
        var response = await TransmitAsync(request, ct);
        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        using (response)
        {
            throw new ApiException(await ProblemReader.ReadAsync(response, ct));
        }
    }

    private async Task<HttpResponseMessage> TransmitAsync(HttpRequestMessage request, CancellationToken ct)
    {
        try
        {
            return await http.SendAsync(request, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new ApiException(ApiError.Unreachable, ex);
        }
    }
}
