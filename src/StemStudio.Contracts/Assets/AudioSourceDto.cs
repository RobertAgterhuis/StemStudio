using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Assets;

/// <summary>An audio input of later features (008-011): a channel of a track, or a library item. Exactly one form is set.</summary>
public sealed record AudioSourceDto
{
    /// <summary>The track (with <see cref="Channel"/>).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? TrackId { get; init; }

    /// <summary>A channel id of the track (with <see cref="TrackId"/>).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Channel { get; init; }

    /// <summary>A library item (alone).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? AssetId { get; init; }

    /// <summary>A channel of a track.</summary>
    /// <param name="trackId">The track.</param>
    /// <param name="channel">The channel id.</param>
    /// <returns>The source.</returns>
    public static AudioSourceDto FromChannel(Guid trackId, string channel) => new() { TrackId = trackId, Channel = channel };

    /// <summary>A library item.</summary>
    /// <param name="assetId">The item.</param>
    /// <returns>The source.</returns>
    public static AudioSourceDto FromAsset(Guid assetId) => new() { AssetId = assetId };
}
