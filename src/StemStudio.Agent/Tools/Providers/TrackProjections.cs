using System.Text.Json.Nodes;
using StemStudio.Contracts.Tracks;
using static StemStudio.Agent.Tools.Providers.ToolDefinitions;

namespace StemStudio.Agent.Tools.Providers;

/// <summary>Whitelisted projections of tracks (titles, artists and errors are external text: untrusted_text only).</summary>
internal static class TrackProjections
{
    /// <summary>The library status of a track: queued, processing, completed or failed.</summary>
    public static string LibraryStatus(TrackStatusDto status) => status switch
    {
        TrackStatusDto.Queued => "queued",
        TrackStatusDto.Completed => "completed",
        TrackStatusDto.Failed => "failed",
        _ => "processing",
    };

    /// <summary>One row of search_library.</summary>
    public static JsonObject Summary(TrackDto track) => new()
    {
        ["trackId"] = track.Id.ToString(),
        [ResultProjector.UntrustedField] = Untrusted(ResultProjector.Join(track.Title, track.Artist)),
        ["status"] = LibraryStatus(track.Status),
        ["durationSeconds"] = track.DurationSeconds,
        ["bpm"] = track.BeatGrid?.Bpm,
        ["hasDetail"] = track.Detail.Status == DetailPhaseStatusDto.Completed,
        ["lyricsStatus"] = Name(track.LyricsStatus),
    };

    /// <summary>The get_track projection.</summary>
    public static JsonObject Details(TrackDto track) => new()
    {
        ["trackId"] = track.Id.ToString(),
        [ResultProjector.UntrustedField] = Untrusted(ResultProjector.Join(track.Title, track.Artist)),
        ["status"] = Name(track.Status),
        ["stage"] = TrustedText.Code(track.Stage),
        ["progress"] = track.Progress,
        ["error"] = UntrustedError(track.Error),
        ["bpm"] = track.BeatGrid?.Bpm,
        ["timeSignature"] = track.BeatGrid is { } grid ? $"{grid.BeatsPerBar}/4" : null,
        ["stems"] = new JsonArray([.. track.Stems.Where(stem => TrustedText.IsChannel(stem.Name)).Select(stem => (JsonNode)JsonValue.Create(stem.Name))]),
        ["detailStatus"] = Name(track.Detail.Status),
        ["device"] = track.Device is { } device ? (device.Kind == ProcessingDeviceKindDto.Gpu ? $"GPU ({TrustedText.SafeName(device.Name) ?? "unknown"})" : "CPU") : null,
        ["lyricsStatus"] = Name(track.LyricsStatus),
        ["metadataStatus"] = Name(track.Metadata.Status),
    };
}
