using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Scores;
using StemStudio.Contracts.Tracks;

namespace StemStudio.Web.Scores;

/// <summary>What the sheet music dialog notates (feature 008): a channel of a track or a ready audio item of the library.</summary>
/// <param name="Source">The source sent to the backend.</param>
/// <param name="Subject">Readable subject, e.g. "bass of Never Gonna Give You Up".</param>
public sealed record ScoreTarget(AudioSourceDto Source, string Subject)
{
    /// <summary>The track whose drums can be split from the dialog (channel sources only).</summary>
    public Guid? TrackId => Source.TrackId;

    /// <summary>A tempo to prefill when the beat grid cannot be used: the track's detected tempo, or null.</summary>
    public ScoreTempoDto? SuggestedTempo { get; init; }

    /// <summary>The capo of capo mode that prefills the guitar tablature options (016 FR-078); 0 without capo mode.</summary>
    public int Capo { get; init; }

    /// <summary>The <c>data-source</c> value: <c>channel</c> or <c>asset</c>.</summary>
    public string SourceKind => Source.AssetId is null ? "channel" : "asset";

    /// <summary>A channel of a completed track.</summary>
    /// <param name="track">The track.</param>
    /// <param name="channel">A stem (<c>bass</c>) or detail channel (<c>vocals.lead</c>).</param>
    /// <returns>The target.</returns>
    public static ScoreTarget ForChannel(TrackDto track, string channel) =>
        new(AudioSourceDto.FromChannel(track.Id, channel), $"{ChannelLabel(channel)} of {track.Title}") { SuggestedTempo = Tempo(track.BeatGrid) };

    /// <summary>A ready audio item of the library.</summary>
    /// <param name="asset">The item.</param>
    /// <returns>The target.</returns>
    public static ScoreTarget ForAsset(AssetDto asset) => new(AudioSourceDto.FromAsset(asset.Id), $"the library item \"{asset.Name}\"");

    /// <summary>"lead (vocals)" for a detail channel, the stem name otherwise.</summary>
    /// <param name="channel">The channel id.</param>
    /// <returns>The label.</returns>
    public static string ChannelLabel(string channel) => channel.IndexOf('.', StringComparison.Ordinal) is var dot and >= 0
        ? $"{channel[(dot + 1)..]} ({channel[..dot]})"
        : channel;

    private static ScoreTempoDto? Tempo(BeatGridDto? grid) => grid is { Bpm: >= 30 and <= 300 }
        ? new ScoreTempoDto(Math.Round(grid.Bpm, 1), Math.Clamp(grid.BeatsPerBar, 1, 12), Math.Round(grid.Downbeats.Count > 0 ? grid.Downbeats[0] : 0, 2))
        : null;
}
