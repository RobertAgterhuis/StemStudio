using StemStudio.Api.Assets;
using StemStudio.Contracts.Assets;
using StemStudio.Domain.Assets;
using StemStudio.Domain.Rendering;
using StemStudio.Domain.Scores;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Scores;

/// <summary>A score source with everything the options, the rules and the worker request need (data-model 4.3, 4.4).</summary>
public sealed record ResolvedSource
{
    /// <summary>The source as requested.</summary>
    public required ScoreSource Source { get; init; }

    /// <summary>The track whose grid is used, when it still exists.</summary>
    public Track? Track { get; init; }

    /// <summary>Track title of the provenance.</summary>
    public required string TrackTitle { get; init; }

    /// <summary>Track artist of the provenance.</summary>
    public string? TrackArtist { get; init; }

    /// <summary>Duration of the source audio.</summary>
    public required double DurationSeconds { get; init; }

    /// <summary>Input path of a pitched profile, relative to the library mount.</summary>
    public required string InputPath { get; init; }

    /// <summary>Kept drum pieces of the track (empty when the drums are not split).</summary>
    public IReadOnlyDictionary<string, string> DrumPieces { get; init; } = new Dictionary<string, string>();

    /// <summary>Start of the source audio item's export range in the track, or null.</summary>
    public double? AssetRangeStart { get; init; }

    /// <summary>The suggestion of a library item (from a single-channel export), else null.</summary>
    public ScoreProfile? AssetSuggestion { get; init; }

    /// <summary>The channel id, or null for a library item.</summary>
    public string? Channel => Source.Channel;
}

/// <summary>Resolves a <c>{trackId, channel}</c> or <c>{assetId}</c> source; problems are 404, 409 <c>track-not-ready</c> or 422.</summary>
/// <param name="tracks">Track repository.</param>
/// <param name="assets">Asset repository.</param>
public sealed class ScoreSourceResolver(ITrackRepository tracks, IAssetRepository assets)
{
    /// <summary>The drum pieces sent to the worker, in contract order.</summary>
    public static readonly string[] DrumPieceNames = ["kick", "snare", "toms", "hihat", "cymbals"];

    /// <summary>Resolves a source.</summary>
    /// <param name="source">The source DTO (exactly one form).</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The source or the problem.</returns>
    public Task<(ResolvedSource? Source, IResult? Problem)> ResolveAsync(AudioSourceDto? source, CancellationToken cancellationToken) => source switch
    {
        { TrackId: { } trackId, Channel: { } channel, AssetId: null } => FromChannelAsync(trackId, channel, cancellationToken),
        { TrackId: null, Channel: null, AssetId: { } assetId } => FromAssetAsync(assetId, cancellationToken),
        _ => Task.FromResult<(ResolvedSource?, IResult?)>((null, ScoreProblems.Field("source", "Give trackId with channel, or assetId."))),
    };

    private async Task<(ResolvedSource?, IResult?)> FromChannelAsync(Guid trackId, string channelId, CancellationToken cancellationToken)
    {
        var track = await tracks.FindAsync(trackId, cancellationToken);
        if (ScoreProblems.TrackProblem(track, trackId) is { } problem)
        {
            return (null, problem);
        }

        var render = RenderSource.Of(track!);
        var channel = RenderChannel.Parse(channelId);
        return channel is not null && render.Has(channel)
            ? (ChannelSource(track!, render, channel), null)
            : (null, ScoreProblems.Field("source.channel", $"The track has no channel {channelId}."));
    }

    private static ResolvedSource ChannelSource(Track track, RenderSource render, RenderChannel channel) => new()
    {
        Source = ScoreSource.FromChannel(track.Id, channel.Id),
        Track = track,
        TrackTitle = track.Title,
        TrackArtist = track.Artist,
        DurationSeconds = track.DurationSeconds ?? 0,
        InputPath = channel.PathFor(track.Id),
        DrumPieces = DrumPiecesOf(track, render, channel),
    };

    /// <summary>A single drum piece reads only its own stem; the whole drums channel reads every kept piece.</summary>
    private static Dictionary<string, string> DrumPiecesOf(Track track, RenderSource render, RenderChannel channel)
    {
        if (ProfileCatalog.DrumPiece(channel.Id) is { } single)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal) { [single] = channel.PathFor(track.Id) };
        }

        return render.Split.TryGetValue("drums", out var pieces)
            ? DrumPieceNames.Where(pieces.Contains).ToDictionary(piece => piece, piece => new RenderChannel("drums", piece).PathFor(track.Id), StringComparer.Ordinal)
            : new Dictionary<string, string>(StringComparer.Ordinal);
    }

    private async Task<(ResolvedSource?, IResult?)> FromAssetAsync(Guid assetId, CancellationToken cancellationToken)
    {
        var asset = await assets.FindAsync(assetId, cancellationToken);
        if (asset is null)
        {
            return (null, AssetProblems.NotFound(assetId));
        }

        if (asset is not { Kind: AssetKind.Audio, Status: AssetStatus.Ready, Audio: not null } || AudioFile(asset) is not { } file)
        {
            return (null, ScoreProblems.Field("source.assetId", "Must be a ready audio item."));
        }

        var track = asset.TrackId is { } trackId ? await tracks.FindAsync(trackId, cancellationToken) : null;
        return (AssetSource(asset, file, track), null);
    }

    private static ResolvedSource AssetSource(Asset asset, AssetFile file, Track? track) => new()
    {
        Source = ScoreSource.FromAsset(asset.Id),
        Track = track,
        TrackTitle = asset.Provenance.TrackTitle,
        TrackArtist = asset.Provenance.TrackArtist,
        DurationSeconds = asset.Audio!.DurationSeconds,
        InputPath = $"assets/{asset.Id:D}/{file.FileName}",
        AssetRangeStart = asset.Provenance.Recipe?.Range?.StartSeconds,
        AssetSuggestion = SuggestionOf(asset.Provenance.Recipe),
    };

    /// <summary>The pitched suggestion of a raw single-channel export (drums need the track's detail stems).</summary>
    private static ScoreProfile? SuggestionOf(ExportRecipe? recipe) =>
        recipe is { Mode: ExportMode.Raw, Channels: [var only] } && ProfileCatalog.Suggest(only) is { } profile && profile != ScoreProfile.Drums ? profile : null;

    private static AssetFile? AudioFile(Asset asset) => asset.Files.FirstOrDefault(file => file.Role == AssetJobApplier.AudioRole);
}
