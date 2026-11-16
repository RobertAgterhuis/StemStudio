using StemStudio.Agent.Ports;
using StemStudio.Contracts;
using StemStudio.Contracts.Artists;
using StemStudio.Contracts.Health;
using StemStudio.Contracts.Importing;
using StemStudio.Contracts.Lyrics;
using StemStudio.Contracts.Mixes;
using StemStudio.Contracts.Tracks;

namespace StemStudio.Api.Agent.Loopback;

/// <summary>Tracks, imports, detail separation and device over loopback (001-003).</summary>
/// <param name="client">Loopback client.</param>
public sealed class LoopbackTrackActions(LoopbackClient client) : ITrackActions
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<TrackDto>> ListTracksAsync(CancellationToken cancellationToken) =>
        await client.GetAsync<List<TrackDto>>(ApiRoutes.Tracks, cancellationToken);

    /// <inheritdoc />
    public Task<TrackDto> GetTrackAsync(Guid trackId, CancellationToken cancellationToken) => client.GetAsync<TrackDto>(ApiRoutes.Track(trackId), cancellationToken);

    /// <inheritdoc />
    public Task<TrackDto> ImportFromUrlAsync(string url, CancellationToken cancellationToken) =>
        client.SendAsync<TrackDto>(HttpMethod.Post, ApiRoutes.Imports, new ImportUrlRequest(url), cancellationToken);

    /// <inheritdoc />
    public Task<TrackDto> RetryTrackAsync(Guid trackId, CancellationToken cancellationToken) =>
        client.SendAsync<TrackDto>(HttpMethod.Post, ApiRoutes.RetryTrack(trackId), null, cancellationToken);

    /// <inheritdoc />
    public Task DeleteTrackAsync(Guid trackId, CancellationToken cancellationToken) => client.SendAsync(HttpMethod.Delete, ApiRoutes.Track(trackId), null, cancellationToken);

    /// <inheritdoc />
    public Task<TrackDto> StartDetailAsync(Guid trackId, CancellationToken cancellationToken) =>
        client.SendAsync<TrackDto>(HttpMethod.Post, ApiRoutes.StartDetail(trackId), null, cancellationToken);

    /// <inheritdoc />
    public Task<TrackDto> CancelDetailAsync(Guid trackId, CancellationToken cancellationToken) =>
        client.SendAsync<TrackDto>(HttpMethod.Post, ApiRoutes.CancelDetail(trackId), null, cancellationToken);

    /// <inheritdoc />
    public Task<ProcessingDeviceDto> GetDeviceAsync(CancellationToken cancellationToken) => client.GetAsync<ProcessingDeviceDto>(ApiRoutes.Device, cancellationToken);
}

/// <summary>Mixes over loopback (001).</summary>
/// <param name="client">Loopback client.</param>
public sealed class LoopbackMixActions(LoopbackClient client) : IMixActions
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<MixSummaryDto>> ListMixesAsync(Guid? trackId, CancellationToken cancellationToken) =>
        await client.GetAsync<List<MixSummaryDto>>(LoopbackQuery.Build(ApiRoutes.Mixes, ("trackId", LoopbackQuery.Id(trackId))), cancellationToken);

    /// <inheritdoc />
    public Task<MixDto> GetMixAsync(Guid mixId, CancellationToken cancellationToken) => client.GetAsync<MixDto>(ApiRoutes.Mix(mixId), cancellationToken);

    /// <inheritdoc />
    public Task<MixDto> CreateMixAsync(CreateMixRequest request, CancellationToken cancellationToken) =>
        client.SendAsync<MixDto>(HttpMethod.Post, ApiRoutes.Mixes, request, cancellationToken);

    /// <inheritdoc />
    public Task<MixDto> UpdateMixAsync(Guid mixId, MixSettingsDto settings, CancellationToken cancellationToken) =>
        client.SendAsync<MixDto>(HttpMethod.Put, ApiRoutes.Mix(mixId), settings, cancellationToken);

    /// <inheritdoc />
    public Task<MixDto> DuplicateMixAsync(Guid mixId, CancellationToken cancellationToken) =>
        client.SendAsync<MixDto>(HttpMethod.Post, ApiRoutes.DuplicateMix(mixId), null, cancellationToken);

    /// <inheritdoc />
    public Task DeleteMixAsync(Guid mixId, CancellationToken cancellationToken) => client.SendAsync(HttpMethod.Delete, ApiRoutes.Mix(mixId), null, cancellationToken);
}

/// <summary>Artwork and band info over loopback (005).</summary>
/// <param name="client">Loopback client.</param>
public sealed class LoopbackArtistActions(LoopbackClient client) : IArtistActions
{
    /// <inheritdoc />
    public Task<BandInfoDto> GetBandInfoAsync(Guid trackId, CancellationToken cancellationToken) => client.GetAsync<BandInfoDto>(ApiRoutes.BandInfo(trackId), cancellationToken);

    /// <inheritdoc />
    public Task<TrackDto> LookupMetadataAsync(Guid trackId, CancellationToken cancellationToken) =>
        client.SendAsync<TrackDto>(HttpMethod.Post, ApiRoutes.MetadataLookup(trackId), null, cancellationToken);
}

/// <summary>Lyrics state over loopback without the text (006).</summary>
/// <param name="client">Loopback client.</param>
public sealed class LoopbackLyricsActions(LoopbackClient client) : ILyricsActions
{
    /// <inheritdoc />
    public async Task<LyricsSummary> GetLyricsSummaryAsync(Guid trackId, CancellationToken cancellationToken) =>
        Summary(await client.GetAsync<LyricsDto>(ApiRoutes.Lyrics(trackId), cancellationToken));

    /// <inheritdoc />
    public async Task<LyricsSummary> SearchLyricsAsync(Guid trackId, CancellationToken cancellationToken) =>
        Summary(await client.SendAsync<LyricsDto>(HttpMethod.Post, ApiRoutes.LyricsSearch(trackId), null, cancellationToken));

    /// <inheritdoc />
    public async Task<LyricsSummary> AlignLyricsAsync(Guid trackId, CancellationToken cancellationToken) =>
        Summary(await client.SendAsync<LyricsDto>(HttpMethod.Post, ApiRoutes.LyricsAlign(trackId), null, cancellationToken));

    /// <inheritdoc />
    public async Task<LyricsSummary> RemoveLyricsAsync(Guid trackId, CancellationToken cancellationToken) =>
        Summary(await client.SendAsync<LyricsDto>(HttpMethod.Delete, ApiRoutes.Lyrics(trackId), null, cancellationToken));

    private static LyricsSummary Summary(LyricsDto lyrics) => new()
    {
        Status = lyrics.Status,
        SyncLevel = lyrics.SyncLevel,
        Origin = lyrics.Origin,
        Source = lyrics.Source,
        Instrumental = lyrics.Instrumental,
        Reason = lyrics.Reason,
    };
}
