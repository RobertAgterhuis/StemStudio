using System.Net;
using StemStudio.Contracts;
using StemStudio.Contracts.Library;
using StemStudio.Web.Library;

namespace StemStudio.Web.Api;

/// <summary>Library folder, search and library info operations (feature 022, backend contract 1.22.0).</summary>
public sealed partial class StemStudioApiClient
{
    /// <inheritdoc />
    public Task<LibraryFolderViewDto?> GetLibraryRootAsync(LibraryFolderRequest request, string? knownRevision = null, CancellationToken cancellationToken = default) =>
        GetFolderViewAsync(ApiRoutes.LibraryFolders + request.ToQueryString(), knownRevision, cancellationToken);

    /// <inheritdoc />
    public Task<LibraryFolderViewDto?> GetLibraryFolderAsync(string folderId, LibraryFolderRequest request, string? knownRevision = null, CancellationToken cancellationToken = default) =>
        GetFolderViewAsync(ApiRoutes.LibraryFolder(Uri.EscapeDataString(folderId)) + request.ToQueryString(), knownRevision, cancellationToken);

    /// <inheritdoc />
    public Task<LibrarySearchResultDto> SearchLibraryAsync(LibrarySearchRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<LibrarySearchResultDto>(HttpMethod.Get, ApiRoutes.LibrarySearch + request.ToQueryString(), null, cancellationToken);

    /// <inheritdoc />
    public Task<LibraryInfoStateDto> GetTrackLibraryInfoAsync(Guid trackId, CancellationToken cancellationToken = default) =>
        SendAsync<LibraryInfoStateDto>(HttpMethod.Get, ApiRoutes.TrackLibraryInfo(trackId), null, cancellationToken);

    /// <inheritdoc />
    public Task<LibraryInfoStateDto> SetTrackLibraryInfoAsync(Guid trackId, LibraryInfoRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<LibraryInfoStateDto>(HttpMethod.Put, ApiRoutes.TrackLibraryInfo(trackId), LibraryInfoBody(request), cancellationToken);

    /// <inheritdoc />
    public Task<LibraryInfoStateDto> ResetTrackLibraryInfoAsync(Guid trackId, CancellationToken cancellationToken = default) =>
        SendAsync<LibraryInfoStateDto>(HttpMethod.Delete, ApiRoutes.TrackLibraryInfo(trackId), null, cancellationToken);

    /// <inheritdoc />
    public Task<LibraryInfoStateDto> GetAssetLibraryInfoAsync(Guid assetId, CancellationToken cancellationToken = default) =>
        SendAsync<LibraryInfoStateDto>(HttpMethod.Get, ApiRoutes.AssetLibraryInfo(assetId), null, cancellationToken);

    /// <inheritdoc />
    public Task<LibraryInfoStateDto> SetAssetLibraryInfoAsync(Guid assetId, LibraryInfoRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<LibraryInfoStateDto>(HttpMethod.Put, ApiRoutes.AssetLibraryInfo(assetId), LibraryInfoBody(request), cancellationToken);

    /// <inheritdoc />
    public Task<LibraryInfoStateDto> ResetAssetLibraryInfoAsync(Guid assetId, CancellationToken cancellationToken = default) =>
        SendAsync<LibraryInfoStateDto>(HttpMethod.Delete, ApiRoutes.AssetLibraryInfo(assetId), null, cancellationToken);

    // The three members are required with explicit nulls (null = automatic), so the body is written with every member.
    private static StringContent LibraryInfoBody(LibraryInfoRequest request) =>
        new(System.Text.Json.JsonSerializer.Serialize(request, Json), System.Text.Encoding.UTF8, "application/json");

    private async Task<LibraryFolderViewDto?> GetFolderViewAsync(string uri, string? knownRevision, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, new Uri(uri, UriKind.Relative));
        if (knownRevision is not null)
        {
            request.Headers.TryAddWithoutValidation("If-None-Match", ApiRoutes.LibraryETag(knownRevision));
        }

        try
        {
            using var response = await _transport.SendRawAsync(request, ct);
            return await response.Content.ReadFromJsonAsync<LibraryFolderViewDto>(Json, ct)
                ?? throw new ApiException(ApiError.FromStatus(response.StatusCode) with { Title = "The server returned an empty response." });
        }
        catch (ApiException ex) when (ex.Error.Status == HttpStatusCode.NotModified)
        {
            return null;
        }
    }
}
