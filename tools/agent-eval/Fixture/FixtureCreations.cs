using StemStudio.Agent.Ports;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Generation;
using StemStudio.Contracts.Scores;
using StemStudio.Contracts.Styles;
using StemStudio.Contracts.Tracks;
using LyricsInput = StemStudio.Contracts.Lyrics.LyricsInput;

namespace StemStudio.AgentEval.Fixture;

internal sealed class FixtureAssets(FixtureLibrary library) : IAssetActions
{
    private readonly Dictionary<Guid, AssetDto> _assets = [];

    public Task<AssetListDto> ListAssetsAsync(AssetQuery query, CancellationToken cancellationToken)
    {
        var items = _assets.Values.Where(asset => query.Kind is null || asset.Kind == query.Kind).ToList();
        return Task.FromResult(new AssetListDto(items, items.Count, items.Sum(asset => asset.SizeBytes)));
    }

    public Task<AssetDto> GetAssetAsync(Guid assetId, CancellationToken cancellationToken) =>
        Task.FromResult(FixtureActions.Found(_assets.GetValueOrDefault(assetId), "asset"));

    public Task<AssetDto> CreateAudioExportAsync(CreateAudioExportRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(Store(AssetKindDto.Audio, AssetOriginDto.Export, request.TrackId, request.Name ?? "Export"));

    public Task<StemPackDto> CreateStemPackAsync(CreateStemPackRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(new StemPackDto([Store(AssetKindDto.Audio, AssetOriginDto.Export, request.TrackId, "Stem pack")]));

    public Task DeleteAssetAsync(Guid assetId, CancellationToken cancellationToken) =>
        _assets.Remove(assetId) ? Task.CompletedTask : throw FixtureActions.NotFound("asset");

    public Task<ScoreOptionsDto> GetScoreOptionsAsync(Guid trackId, string channel, CancellationToken cancellationToken) => Task.FromResult(new ScoreOptionsDto
    {
        Source = AudioSourceDto.FromChannel(trackId, channel),
        DurationSeconds = 260,
        SuggestedProfile = Enum.TryParse<ScoreProfileDto>(channel.Split('.')[0], ignoreCase: true, out var profile) ? profile : null,
        AllowedProfiles = Enum.GetValues<ScoreProfileDto>(),
        GridStatus = GridStatusDto.Reliable,
        DefaultQuality = ScoreQualityDto.Accurate,
        Estimates = [.. Enum.GetValues<ScoreProfileDto>().Select(item => new ScoreEstimateDto(item, null, 90))],
        Attribution = "evaluation fixture",
    });

    public Task<AssetDto> CreateScoreAsync(CreateScoreRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(Store(AssetKindDto.Score, AssetOriginDto.Transcription, request.Source.TrackId ?? Guid.Empty, request.Name ?? "Score"));

    private AssetDto Store(AssetKindDto kind, AssetOriginDto origin, Guid trackId, string name)
    {
        var track = library.Tracks.GetValueOrDefault(trackId) ?? throw FixtureActions.NotFound("track");
        var asset = Ready(kind, name, new ProvenanceDto { Origin = origin, TrackTitle = track.Title, TrackAvailable = true, Contents = "fixture" });
        _assets[asset.Id] = asset;
        return asset;
    }

    private static AssetDto Ready(AssetKindDto kind, string name, ProvenanceDto provenance) => new()
    {
        Id = Guid.NewGuid(),
        Kind = kind,
        Name = name,
        Status = AssetStatusDto.Ready,
        Progress = 100,
        Provenance = provenance,
        SizeBytes = 40_000_000,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
    };
}

internal sealed class FixtureStyles(FixtureLibrary library) : IStyleActions
{
    private readonly List<StyleDto> _user = [];

    public Task<IReadOnlyList<StyleDto>> ListStylesAsync(StyleQuery query, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<StyleDto>>([.. library.Styles.Concat(_user).Where(style => Matches(style, query))]);

    public Task<StyleDto> CreateStyleAsync(StyleInput input, CancellationToken cancellationToken)
    {
        var template = library.Styles[0];
        var style = template with { Id = Guid.NewGuid(), Kind = StyleKindDto.User, PresetKey = null, Name = input.Name, GenreFamily = input.GenreFamily };
        _user.Add(style);
        return Task.FromResult(style);
    }

    public Task DeleteStyleAsync(Guid styleId, CancellationToken cancellationToken) =>
        _user.RemoveAll(style => style.Id == styleId) > 0 ? Task.CompletedTask : throw FixtureActions.NotFound("style");

    private static bool Matches(StyleDto style, StyleQuery query) =>
        (query.Kind is null || style.Kind == query.Kind) && (query.GenreFamily is null || style.GenreFamily == query.GenreFamily) && TextMatches(style, query.Query);

    private static bool TextMatches(StyleDto style, string? query) =>
        string.IsNullOrWhiteSpace(query) || $"{style.Name} {style.PresetKey} {style.SubGenre}".Contains(query, StringComparison.OrdinalIgnoreCase);
}

internal sealed class FixtureGenerations(FixtureLibrary library) : IGenerationActions
{
    public Task<GenerationStatusDto> GetStatusAsync(CancellationToken cancellationToken) => Task.FromResult(new GenerationStatusDto
    {
        State = GenerationAvailabilityDto.Ready,
        Voice = new VoiceServiceStatusDto { State = GenerationAvailabilityDto.Ready },
    });

    public Task<GenerationDto> CreateRestyleAsync(Guid trackId, RestyleRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(FixtureGenerations.Add(library, GenerationKindDto.Restyle));

    public Task<IReadOnlyList<GenerationDto>> ListGenerationsAsync(GenerationQuery query, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<GenerationDto>>([.. library.Generations.Where(item => query.Kind is null || item.Kind == query.Kind)]);

    public Task<GenerationDto> GetGenerationAsync(Guid generationId, CancellationToken cancellationToken) =>
        Task.FromResult(FixtureActions.Found(library.Generations.Find(item => item.Id == generationId), "generation"));

    public Task<GenerationDto> CancelGenerationAsync(Guid generationId, CancellationToken cancellationToken) => GetGenerationAsync(generationId, cancellationToken);

    public Task<GenerationDto> RetryGenerationAsync(Guid generationId, CancellationToken cancellationToken) => GetGenerationAsync(generationId, cancellationToken);

    public Task DeleteGenerationAsync(Guid generationId, CancellationToken cancellationToken) =>
        library.Generations.RemoveAll(item => item.Id == generationId) > 0 ? Task.CompletedTask : throw FixtureActions.NotFound("generation");

    public Task<TrackDto> ImportTakeAsync(Guid generationId, int index, CancellationToken cancellationToken)
    {
        var track = FixtureLibrary.Track(Guid.NewGuid(), $"Take {index}", null);
        library.Tracks[track.Id] = track;
        return Task.FromResult(track);
    }

    /// <summary>A completed generation of the kind (jobs of the fixture finish at once).</summary>
    public static GenerationDto Add(FixtureLibrary library, GenerationKindDto kind)
    {
        var now = DateTimeOffset.UtcNow;
        var generation = new GenerationDto { Id = Guid.NewGuid(), Kind = kind, Status = GenerationStateDto.Completed, Progress = 100, CreatedAt = now, UpdatedAt = now, CompletedAt = now };
        library.Generations.Add(generation);
        return generation;
    }
}

internal sealed class FixtureSongs(FixtureLibrary library) : ISongActions
{
    private readonly List<LyricsSheetSummary> _sheets =
        [.. FixtureLyricAssistActions.Sheets.Select(sheet => new LyricsSheetSummary { LyricsId = library.Ids.Of(sheet.Key), Title = sheet.Title, Language = sheet.Language, UpdatedAt = DateTimeOffset.UnixEpoch })];

    public Task<IReadOnlyList<LyricsSheetSummary>> ListLyricsSheetsAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<LyricsSheetSummary>>([.. _sheets]);

    public Task<LyricsSheetSummary> CreateLyricsSheetAsync(LyricsInput input, CancellationToken cancellationToken)
    {
        var sheet = new LyricsSheetSummary { LyricsId = Guid.NewGuid(), Title = input.Title, Language = input.Language, UpdatedAt = DateTimeOffset.UtcNow };
        _sheets.Add(sheet);
        return Task.FromResult(sheet);
    }

    public Task DeleteLyricsSheetAsync(Guid lyricsId, CancellationToken cancellationToken) =>
        _sheets.RemoveAll(sheet => sheet.LyricsId == lyricsId) > 0 ? Task.CompletedTask : throw FixtureActions.NotFound("lyrics sheet");

    public Task<GenerationDto> CreateSongAsync(SongRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(FixtureGenerations.Add(library, GenerationKindDto.Song));
}

internal sealed class FixtureVoices(FixtureLibrary library) : IVoiceActions
{
    public Task<IReadOnlyList<VoiceProfileSummary>> ListVoiceProfilesAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<VoiceProfileSummary>>([.. library.Voices]);

    public Task<GenerationDto> CreateVoiceConversionAsync(Guid generationId, int index, VoiceConversionRequest request, CancellationToken cancellationToken)
    {
        _ = library.Voices.Find(voice => voice.ProfileId == request.ProfileId) ?? throw new AgentActionException(409, "consent-missing", "The voice profile does not exist.");
        return Task.FromResult(FixtureGenerations.Add(library, GenerationKindDto.VoiceConversion));
    }
}
