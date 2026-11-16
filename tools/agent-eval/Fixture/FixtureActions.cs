using StemStudio.Agent.Ports;
using StemStudio.Contracts.Artists;
using StemStudio.Contracts.Health;
using StemStudio.Contracts.Lyrics;
using StemStudio.Contracts.Mixes;
using StemStudio.Contracts.Tracks;

namespace StemStudio.AgentEval.Fixture;

/// <summary>
/// The StemStudio actions over the seeded fixture: reads return the fixture, writes change it, and every job (import,
/// detail separation, export, score, restyle, song, voice conversion) completes at once so <c>wait_for_job</c> ends on its
/// first poll. Nothing leaves the process.
/// </summary>
internal sealed class FixtureActions(FixtureLibrary library) : IStemStudioActions
{
    public ITrackActions Tracks { get; } = new FixtureTracks(library);

    public IMixActions Mixes { get; } = new FixtureMixes(library);

    public IArtistActions Artists { get; } = new FixtureArtists(library);

    public ILyricsActions Lyrics { get; } = new FixtureLyrics();

    public IAssetActions Assets { get; } = new FixtureAssets(library);

    public IStyleActions Styles { get; } = new FixtureStyles(library);

    public IGenerationActions Generations { get; } = new FixtureGenerations(library);

    public ISongActions Songs { get; } = new FixtureSongs(library);

    public IVoiceActions Voices { get; } = new FixtureVoices(library);

    /// <summary>The 404 of a missing fixture item.</summary>
    public static AgentActionException NotFound(string what) => new(404, "not-found", $"The {what} does not exist.");

    /// <summary>The value, or a 404 when it is missing.</summary>
    public static T Found<T>(T? value, string what)
        where T : class => value ?? throw NotFound(what);
}

internal sealed class FixtureTracks(FixtureLibrary library) : ITrackActions
{
    public Task<IReadOnlyList<TrackDto>> ListTracksAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<TrackDto>>([.. library.Tracks.Values]);

    public Task<TrackDto> GetTrackAsync(Guid trackId, CancellationToken cancellationToken) =>
        Task.FromResult(FixtureActions.Found(library.Tracks.GetValueOrDefault(trackId), "track"));

    public Task<TrackDto> ImportFromUrlAsync(string url, CancellationToken cancellationToken)
    {
        var track = FixtureLibrary.Track(Guid.NewGuid(), "Imported track", null) with { SourceUrl = url };
        library.Tracks[track.Id] = track;
        return Task.FromResult(track);
    }

    public Task<TrackDto> RetryTrackAsync(Guid trackId, CancellationToken cancellationToken) =>
        Replace(trackId, track => FixtureLibrary.Track(track.Id, track.Title, track.Artist));

    public Task DeleteTrackAsync(Guid trackId, CancellationToken cancellationToken) =>
        library.Tracks.Remove(trackId) ? Task.CompletedTask : throw FixtureActions.NotFound("track");

    public Task<TrackDto> StartDetailAsync(Guid trackId, CancellationToken cancellationToken) =>
        Replace(trackId, track => track with { Detail = Detail(DetailPhaseStatusDto.Completed, 100) });

    public Task<TrackDto> CancelDetailAsync(Guid trackId, CancellationToken cancellationToken) =>
        Replace(trackId, track => track with { Detail = Detail(DetailPhaseStatusDto.Cancelled, 0) });

    public Task<ProcessingDeviceDto> GetDeviceAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new ProcessingDeviceDto { Reachable = true, Kind = ProcessingDeviceKindDto.Gpu, Name = "NVIDIA GeForce RTX 4070", TotalMemoryMiB = 12_282 });

    private static DetailPhaseDto Detail(DetailPhaseStatusDto status, int progress) => new() { Status = status, Progress = progress, Parents = [] };

    private Task<TrackDto> Replace(Guid trackId, Func<TrackDto, TrackDto> change)
    {
        var track = change(FixtureActions.Found(library.Tracks.GetValueOrDefault(trackId), "track"));
        library.Tracks[trackId] = track;
        return Task.FromResult(track);
    }
}

internal sealed class FixtureMixes(FixtureLibrary library) : IMixActions
{
    public Task<IReadOnlyList<MixSummaryDto>> ListMixesAsync(Guid? trackId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<MixSummaryDto>>([.. library.Mixes.Values.Where(mix => trackId is null || mix.TrackId == trackId).Select(Summary)]);

    public Task<MixDto> GetMixAsync(Guid mixId, CancellationToken cancellationToken) =>
        Task.FromResult(FixtureActions.Found(library.Mixes.GetValueOrDefault(mixId), "mix"));

    public Task<MixDto> CreateMixAsync(CreateMixRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(Store(new MixDto(Guid.NewGuid(), request.TrackId, request.Settings, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)));

    public async Task<MixDto> UpdateMixAsync(Guid mixId, MixSettingsDto settings, CancellationToken cancellationToken) =>
        Store(await GetMixAsync(mixId, cancellationToken) with { Settings = settings, UpdatedAt = DateTimeOffset.UtcNow });

    public async Task<MixDto> DuplicateMixAsync(Guid mixId, CancellationToken cancellationToken)
    {
        var mix = await GetMixAsync(mixId, cancellationToken);
        return Store(mix with { Id = Guid.NewGuid(), Settings = mix.Settings with { Name = mix.Settings.Name + " (copy)" } });
    }

    public Task DeleteMixAsync(Guid mixId, CancellationToken cancellationToken) =>
        library.Mixes.Remove(mixId) ? Task.CompletedTask : throw FixtureActions.NotFound("mix");

    private MixSummaryDto Summary(MixDto mix) =>
        new(mix.Id, mix.TrackId, library.Tracks.GetValueOrDefault(mix.TrackId)?.Title ?? "(deleted)", mix.Settings.Name, mix.UpdatedAt);

    private MixDto Store(MixDto mix)
    {
        library.Mixes[mix.Id] = mix;
        return mix;
    }
}

internal sealed class FixtureArtists(FixtureLibrary library) : IArtistActions
{
    public Task<BandInfoDto> GetBandInfoAsync(Guid trackId, CancellationToken cancellationToken) =>
        Task.FromResult(new BandInfoDto { Status = MetadataStatusDto.None, Reason = "No band information in the evaluation fixture." });

    public Task<TrackDto> LookupMetadataAsync(Guid trackId, CancellationToken cancellationToken) =>
        Task.FromResult(FixtureActions.Found(library.Tracks.GetValueOrDefault(trackId), "track"));
}

internal sealed class FixtureLyrics : ILyricsActions
{
    private static readonly LyricsSummary None = new() { Status = LyricsStatusDto.None, Instrumental = false };

    public Task<LyricsSummary> GetLyricsSummaryAsync(Guid trackId, CancellationToken cancellationToken) => Task.FromResult(None);

    public Task<LyricsSummary> SearchLyricsAsync(Guid trackId, CancellationToken cancellationToken) =>
        Task.FromResult(None with { Status = LyricsStatusDto.Found });

    public Task<LyricsSummary> AlignLyricsAsync(Guid trackId, CancellationToken cancellationToken) =>
        Task.FromResult(None with { Status = LyricsStatusDto.Found });

    public Task<LyricsSummary> RemoveLyricsAsync(Guid trackId, CancellationToken cancellationToken) => Task.FromResult(None);
}
