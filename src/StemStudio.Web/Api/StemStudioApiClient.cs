using System.Net.Http.Headers;
using System.Text.Json;
using StemStudio.Contracts;
using StemStudio.Contracts.Artists;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Health;
using StemStudio.Contracts.Importing;
using StemStudio.Contracts.Lyrics;
using StemStudio.Contracts.Mixes;
using StemStudio.Contracts.Scores;
using StemStudio.Contracts.Tracks;

namespace StemStudio.Web.Api;

/// <summary>
/// <see cref="HttpClient"/>-based implementation of <see cref="IStemStudioApiClient"/>, registered as a
/// typed client on <see cref="StemStudioWebOptions.ApiClientName"/> with the server-side API base URL.
/// </summary>
/// <param name="http">The configured HTTP client.</param>
public sealed partial class StemStudioApiClient(HttpClient http) : IStemStudioApiClient
{
    /// <summary>JSON options of the backend contract (camelCase; enums carry their own converters).</summary>
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly ApiTransport _transport = new(http, Json);

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
        SendAsync<ProcessingDeviceDto>(HttpMethod.Get, ApiRoutes.Device, null, cancellationToken);

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

    /// <inheritdoc />
    public Task<BandInfoDto> GetBandInfoAsync(Guid trackId, CancellationToken cancellationToken = default) =>
        SendAsync<BandInfoDto>(HttpMethod.Get, ApiRoutes.BandInfo(trackId), null, cancellationToken);

    /// <inheritdoc />
    public Task<TrackDto> LookupMetadataAsync(Guid trackId, MetadataLookupRequest? request, CancellationToken cancellationToken = default) =>
        SendAsync<TrackDto>(HttpMethod.Post, ApiRoutes.MetadataLookup(trackId), request is null ? JsonBody(new { }) : JsonBody(request), cancellationToken);

    /// <inheritdoc />
    public Task<TrackDto> SetArtworkAsync(Guid trackId, UploadFile file, CancellationToken cancellationToken = default) =>
        SendAsync<TrackDto>(HttpMethod.Put, ApiRoutes.TrackArtworkUpload(trackId), MultipartBody(file), cancellationToken);

    /// <inheritdoc />
    public Task<TrackDto> ResetArtworkAsync(Guid trackId, CancellationToken cancellationToken = default) =>
        SendAsync<TrackDto>(HttpMethod.Delete, ApiRoutes.TrackArtworkUpload(trackId), null, cancellationToken);

    /// <inheritdoc />
    public Task<AssetListDto> ListAssetsAsync(CancellationToken cancellationToken = default) =>
        SendAsync<AssetListDto>(HttpMethod.Get, $"{ApiRoutes.Assets}?limit={ContractLimits.MaxAssetPageSize}", null, cancellationToken);

    /// <inheritdoc />
    public Task<AssetDto> GetAssetAsync(Guid assetId, CancellationToken cancellationToken = default) =>
        SendAsync<AssetDto>(HttpMethod.Get, ApiRoutes.Asset(assetId), null, cancellationToken);

    /// <inheritdoc />
    public Task<AssetDto> CreateAudioExportAsync(CreateAudioExportRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<AssetDto>(HttpMethod.Post, ApiRoutes.AudioExports, JsonBody(request), cancellationToken);

    /// <inheritdoc />
    public Task<StemPackDto> CreateStemPackAsync(CreateStemPackRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<StemPackDto>(HttpMethod.Post, ApiRoutes.StemPacks, JsonBody(request), cancellationToken);

    /// <inheritdoc />
    public Task<AssetDto> RenameAssetAsync(Guid assetId, string name, CancellationToken cancellationToken = default) =>
        SendAsync<AssetDto>(HttpMethod.Patch, ApiRoutes.Asset(assetId), JsonBody(new RenameAssetRequest(name)), cancellationToken);

    /// <inheritdoc />
    public async Task DeleteAssetAsync(Guid assetId, CancellationToken cancellationToken = default)
    {
        using var response = await SendRawAsync(HttpMethod.Delete, ApiRoutes.Asset(assetId), null, cancellationToken);
    }

    /// <inheritdoc />
    public Task<AssetDto> RetryAssetAsync(Guid assetId, CancellationToken cancellationToken = default) =>
        SendAsync<AssetDto>(HttpMethod.Post, ApiRoutes.RetryAsset(assetId), null, cancellationToken);

    /// <inheritdoc />
    public Task<LyricsDto> GetLyricsAsync(Guid trackId, CancellationToken cancellationToken = default) =>
        SendAsync<LyricsDto>(HttpMethod.Get, ApiRoutes.Lyrics(trackId), null, cancellationToken);

    /// <inheritdoc />
    public Task<LyricsDto> SetLyricsOffsetAsync(Guid trackId, int offsetMs, CancellationToken cancellationToken = default) =>
        SendAsync<LyricsDto>(HttpMethod.Put, ApiRoutes.LyricsOffset(trackId), JsonBody(new LyricsOffsetRequest(offsetMs)), cancellationToken);

    /// <inheritdoc />
    public Task<LyricsDto> SetLyricsAsync(Guid trackId, SetLyricsRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<LyricsDto>(HttpMethod.Put, ApiRoutes.Lyrics(trackId), JsonBody(request), cancellationToken);

    /// <inheritdoc />
    public Task<LyricsDto> AlignLyricsAsync(Guid trackId, CancellationToken cancellationToken = default) =>
        SendAsync<LyricsDto>(HttpMethod.Post, ApiRoutes.LyricsAlign(trackId), null, cancellationToken);

    /// <inheritdoc />
    public Task<LyricsDto> CancelLyricsAlignmentAsync(Guid trackId, CancellationToken cancellationToken = default) =>
        SendAsync<LyricsDto>(HttpMethod.Post, ApiRoutes.LyricsAlignCancel(trackId), null, cancellationToken);

    /// <inheritdoc />
    public Task<LyricsDto> SearchLyricsAsync(Guid trackId, CancellationToken cancellationToken = default) =>
        SendAsync<LyricsDto>(HttpMethod.Post, ApiRoutes.LyricsSearch(trackId), null, cancellationToken);

    /// <inheritdoc />
    public Task<LyricsDto> RemoveLyricsAsync(Guid trackId, CancellationToken cancellationToken = default) =>
        SendAsync<LyricsDto>(HttpMethod.Delete, ApiRoutes.Lyrics(trackId), null, cancellationToken);

    /// <inheritdoc />
    public Task<ScoreOptionsDto> GetScoreOptionsAsync(AudioSourceDto source, CancellationToken cancellationToken = default) =>
        SendAsync<ScoreOptionsDto>(HttpMethod.Get, ScoreOptionsQuery(source), null, cancellationToken);

    /// <inheritdoc />
    public Task<AssetDto> CreateScoreAsync(CreateScoreRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<AssetDto>(HttpMethod.Post, ApiRoutes.Scores, JsonBody(request), cancellationToken);

    private static string ScoreOptionsQuery(AudioSourceDto source) =>
        source.AssetId is { } assetId ? ApiRoutes.ScoreOptionsForAsset(assetId) : ApiRoutes.ScoreOptionsForChannel(source.TrackId!.Value, source.Channel!);

    private static string MixesQuery(Guid? trackId) =>
        trackId is { } id ? $"{ApiRoutes.Mixes}?trackId={id}" : ApiRoutes.Mixes;

    private static JsonContent JsonBody<T>(T value) => JsonContent.Create(value, options: Json);

    private static MultipartFormDataContent MultipartBody(UploadFile file, string name = "file")
    {
        var stream = new StreamContent(file.Content);
        // The recorder's type carries parameters ("audio/webm;codecs=opus"); browsers give "" for unknown extensions (.lrc).
        stream.Headers.ContentType = MediaTypeHeaderValue.TryParse(file.ContentType, out var type)
            ? type
            : new MediaTypeHeaderValue("application/octet-stream");
        return new MultipartFormDataContent { { stream, name, file.FileName } };
    }

    private Task<T> SendAsync<T>(HttpMethod method, string uri, HttpContent? content, CancellationToken ct) =>
        _transport.SendAsync<T>(method, uri, content, ct);

    private Task<HttpResponseMessage> SendRawAsync(HttpMethod method, string uri, HttpContent? content, CancellationToken ct) =>
        _transport.SendRawAsync(new HttpRequestMessage(method, new Uri(uri, UriKind.Relative)) { Content = content }, ct);
}
