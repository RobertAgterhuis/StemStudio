using StemStudio.Agent.Ports;
using StemStudio.Contracts;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Generation;
using StemStudio.Contracts.Lyrics;
using StemStudio.Contracts.Scores;
using StemStudio.Contracts.Styles;
using StemStudio.Contracts.Tracks;
using StemStudio.Contracts.Voices;

namespace StemStudio.Api.Agent.Loopback;

/// <summary>Exports and scores in the asset library over loopback (007, 008).</summary>
/// <param name="client">Loopback client.</param>
public sealed class LoopbackAssetActions(LoopbackClient client) : IAssetActions
{
    /// <inheritdoc />
    public Task<AssetListDto> ListAssetsAsync(AssetQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var path = LoopbackQuery.Build(ApiRoutes.Assets, ("kind", LoopbackQuery.Name(query.Kind)), ("trackId", LoopbackQuery.Id(query.TrackId)),
            ("status", LoopbackQuery.Name(query.Status)), ("q", query.Query), ("limit", LoopbackQuery.Number(query.Limit)));
        return client.GetAsync<AssetListDto>(path, cancellationToken);
    }

    /// <inheritdoc />
    public Task<AssetDto> GetAssetAsync(Guid assetId, CancellationToken cancellationToken) => client.GetAsync<AssetDto>(ApiRoutes.Asset(assetId), cancellationToken);

    /// <inheritdoc />
    public Task<AssetDto> CreateAudioExportAsync(CreateAudioExportRequest request, CancellationToken cancellationToken) =>
        client.SendAsync<AssetDto>(HttpMethod.Post, ApiRoutes.AudioExports, request, cancellationToken);

    /// <inheritdoc />
    public Task<StemPackDto> CreateStemPackAsync(CreateStemPackRequest request, CancellationToken cancellationToken) =>
        client.SendAsync<StemPackDto>(HttpMethod.Post, ApiRoutes.StemPacks, request, cancellationToken);

    /// <inheritdoc />
    public Task DeleteAssetAsync(Guid assetId, CancellationToken cancellationToken) => client.SendAsync(HttpMethod.Delete, ApiRoutes.Asset(assetId), null, cancellationToken);

    /// <inheritdoc />
    public Task<ScoreOptionsDto> GetScoreOptionsAsync(Guid trackId, string channel, CancellationToken cancellationToken) =>
        client.GetAsync<ScoreOptionsDto>(ApiRoutes.ScoreOptionsForChannel(trackId, channel), cancellationToken);

    /// <inheritdoc />
    public Task<AssetDto> CreateScoreAsync(CreateScoreRequest request, CancellationToken cancellationToken) =>
        client.SendAsync<AssetDto>(HttpMethod.Post, ApiRoutes.Scores, request, cancellationToken);
}

/// <summary>The style library over loopback (009); the backend's imitation guard stays authoritative.</summary>
/// <param name="client">Loopback client.</param>
public sealed class LoopbackStyleActions(LoopbackClient client) : IStyleActions
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<StyleDto>> ListStylesAsync(StyleQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var path = LoopbackQuery.Build(ApiRoutes.Styles, ("q", query.Query), ("kind", LoopbackQuery.Name(query.Kind)), ("genreFamily", LoopbackQuery.Name(query.GenreFamily)));
        return await client.GetAsync<List<StyleDto>>(path, cancellationToken);
    }

    /// <inheritdoc />
    public Task<StyleDto> CreateStyleAsync(StyleInput input, CancellationToken cancellationToken) =>
        client.SendAsync<StyleDto>(HttpMethod.Post, ApiRoutes.Styles, input, cancellationToken);

    /// <inheritdoc />
    public Task DeleteStyleAsync(Guid styleId, CancellationToken cancellationToken) => client.SendAsync(HttpMethod.Delete, ApiRoutes.Style(styleId), null, cancellationToken);
}

/// <summary>Generation status, restyles and generations over loopback (009, 010).</summary>
/// <param name="client">Loopback client.</param>
public sealed class LoopbackGenerationActions(LoopbackClient client) : IGenerationActions
{
    /// <inheritdoc />
    public Task<GenerationStatusDto> GetStatusAsync(CancellationToken cancellationToken) => client.GetAsync<GenerationStatusDto>(ApiRoutes.GenerationStatus, cancellationToken);

    /// <inheritdoc />
    public Task<GenerationDto> CreateRestyleAsync(Guid trackId, RestyleRequest request, CancellationToken cancellationToken) =>
        client.SendAsync<GenerationDto>(HttpMethod.Post, ApiRoutes.Restyles(trackId), request, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<GenerationDto>> ListGenerationsAsync(GenerationQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var path = LoopbackQuery.Build(ApiRoutes.Generations, ("kind", LoopbackQuery.Name(query.Kind)), ("sourceTrackId", LoopbackQuery.Id(query.SourceTrackId)));
        return await client.GetAsync<List<GenerationDto>>(path, cancellationToken);
    }

    /// <inheritdoc />
    public Task<GenerationDto> GetGenerationAsync(Guid generationId, CancellationToken cancellationToken) =>
        client.GetAsync<GenerationDto>(ApiRoutes.Generation(generationId), cancellationToken);

    /// <inheritdoc />
    public Task<GenerationDto> CancelGenerationAsync(Guid generationId, CancellationToken cancellationToken) =>
        client.SendAsync<GenerationDto>(HttpMethod.Post, ApiRoutes.CancelGeneration(generationId), null, cancellationToken);

    /// <inheritdoc />
    public Task<GenerationDto> RetryGenerationAsync(Guid generationId, CancellationToken cancellationToken) =>
        client.SendAsync<GenerationDto>(HttpMethod.Post, ApiRoutes.RetryGeneration(generationId), null, cancellationToken);

    /// <inheritdoc />
    public Task DeleteGenerationAsync(Guid generationId, CancellationToken cancellationToken) =>
        client.SendAsync(HttpMethod.Delete, ApiRoutes.Generation(generationId), null, cancellationToken);

    /// <inheritdoc />
    public Task<TrackDto> ImportTakeAsync(Guid generationId, int index, CancellationToken cancellationToken) =>
        client.SendAsync<TrackDto>(HttpMethod.Post, ApiRoutes.ImportTake(generationId, index), null, cancellationToken);
}

/// <summary>Own lyrics sheets (never their text) and songs over loopback (010).</summary>
/// <param name="client">Loopback client.</param>
public sealed class LoopbackSongActions(LoopbackClient client) : ISongActions
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<LyricsSheetSummary>> ListLyricsSheetsAsync(CancellationToken cancellationToken) =>
        [.. (await client.GetAsync<List<LyricsSheetDto>>(ApiRoutes.LyricsSheets, cancellationToken)).Select(Summary)];

    /// <inheritdoc />
    public async Task<LyricsSheetSummary> CreateLyricsSheetAsync(LyricsInput input, CancellationToken cancellationToken) =>
        Summary(await client.SendAsync<LyricsSheetDto>(HttpMethod.Post, ApiRoutes.LyricsSheets, input, cancellationToken));

    /// <inheritdoc />
    public Task DeleteLyricsSheetAsync(Guid lyricsId, CancellationToken cancellationToken) =>
        client.SendAsync(HttpMethod.Delete, ApiRoutes.LyricsSheet(lyricsId), null, cancellationToken);

    /// <inheritdoc />
    public Task<GenerationDto> CreateSongAsync(SongRequest request, CancellationToken cancellationToken) =>
        client.SendAsync<GenerationDto>(HttpMethod.Post, ApiRoutes.Songs, request, cancellationToken);

    private static LyricsSheetSummary Summary(LyricsSheetDto sheet) =>
        new() { LyricsId = sheet.Id, Title = sheet.Title, Language = sheet.Language, UpdatedAt = sheet.UpdatedAt };
}

/// <summary>Own-voice profiles (read only, no personal data) and voice conversions over loopback (010).</summary>
/// <param name="client">Loopback client.</param>
public sealed class LoopbackVoiceActions(LoopbackClient client) : IVoiceActions
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<VoiceProfileSummary>> ListVoiceProfilesAsync(CancellationToken cancellationToken) =>
        [.. (await client.GetAsync<List<VoiceProfileDto>>(ApiRoutes.VoiceProfiles, cancellationToken)).Select(Summary)];

    /// <inheritdoc />
    public Task<GenerationDto> CreateVoiceConversionAsync(Guid generationId, int index, VoiceConversionRequest request, CancellationToken cancellationToken) =>
        client.SendAsync<GenerationDto>(HttpMethod.Post, ApiRoutes.VoiceConversions(generationId, index), request, cancellationToken);

    private static VoiceProfileSummary Summary(VoiceProfileDto profile) => new()
    {
        ProfileId = profile.Id,
        DisplayName = profile.DisplayName,
        ConsentStatementVersion = profile.Consent.StatementVersion,
        DurationSeconds = profile.DurationSeconds,
    };
}
