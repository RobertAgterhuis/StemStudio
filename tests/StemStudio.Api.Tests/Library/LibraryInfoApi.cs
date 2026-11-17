using System.Net.Http.Json;
using StemStudio.Contracts;
using StemStudio.Contracts.Library;

namespace StemStudio.Api.Tests.Library;

/// <summary>HTTP helpers of the library-info tests (backend-api 1.22.0).</summary>
internal static class LibraryInfoApi
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static LibraryInfoRequest Names(string? artist = null, string? album = null, string? song = null) => new() { Artist = artist, Album = album, Song = song };

    public static Task<HttpResponseMessage> GetTrackInfoAsync(this HttpClient client, Guid trackId) => client.GetAsync(new Uri(ApiRoutes.TrackLibraryInfo(trackId), UriKind.Relative), Ct);

    public static Task<HttpResponseMessage> PutTrackInfoAsync(this HttpClient client, Guid trackId, LibraryInfoRequest request) =>
        client.PutAsJsonAsync(new Uri(ApiRoutes.TrackLibraryInfo(trackId), UriKind.Relative), request, Ct);

    public static Task<HttpResponseMessage> DeleteTrackInfoAsync(this HttpClient client, Guid trackId) => client.DeleteAsync(new Uri(ApiRoutes.TrackLibraryInfo(trackId), UriKind.Relative), Ct);

    public static Task<HttpResponseMessage> GetItemInfoAsync(this HttpClient client, Guid assetId) => client.GetAsync(new Uri(ApiRoutes.AssetLibraryInfo(assetId), UriKind.Relative), Ct);

    public static Task<HttpResponseMessage> PutItemInfoAsync(this HttpClient client, Guid assetId, LibraryInfoRequest request) =>
        client.PutAsJsonAsync(new Uri(ApiRoutes.AssetLibraryInfo(assetId), UriKind.Relative), request, Ct);

    public static Task<HttpResponseMessage> DeleteItemInfoAsync(this HttpClient client, Guid assetId) => client.DeleteAsync(new Uri(ApiRoutes.AssetLibraryInfo(assetId), UriKind.Relative), Ct);

    public static (string? Value, string Effective, PlacementSourceDto Source, string Automatic) Of(LibraryInfoLevelDto level) =>
        (level.Value, level.Effective, level.Source, level.Automatic);
}
