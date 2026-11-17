using System.Text.Json.Serialization;
using StemStudio.Contracts.Lyrics;

namespace StemStudio.Contracts.Tracks;

/// <summary>A track with its processing state, stems and analysis.</summary>
public sealed record TrackDto
{
    /// <summary>Track identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>Title from the source metadata or the uploaded file name.</summary>
    public required string Title { get; init; }

    /// <summary>Artist, when known.</summary>
    public string? Artist { get; init; }

    /// <summary>Source type.</summary>
    public required SourceTypeDto SourceType { get; init; }

    /// <summary>Cache identity: <c>youtube:&lt;videoId&gt;</c>, <c>spotify:&lt;trackId&gt;</c> or <c>upload:&lt;sha256&gt;</c>.</summary>
    public required string SourceKey { get; init; }

    /// <summary>The URL the user imported (null for uploads).</summary>
    public string? SourceUrl { get; init; }

    /// <summary>Processing status.</summary>
    public required TrackStatusDto Status { get; init; }

    /// <summary>Human-readable stage, e.g. "Separating stems".</summary>
    public string? Stage { get; init; }

    /// <summary>Overall progress 0..100.</summary>
    public required int Progress { get; init; }

    /// <summary>Readable failure reason when <see cref="Status"/> is failed.</summary>
    public string? Error { get; init; }

    /// <summary>Duration in seconds once known.</summary>
    public double? DurationSeconds { get; init; }

    /// <summary>Separation model used: <c>htdemucs_6s</c>, <c>htdemucs</c> or <c>fake</c>.</summary>
    public string? Model { get; init; }

    /// <summary>Available stems in display order (empty until completed).</summary>
    public required IReadOnlyList<StemDto> Stems { get; init; }

    /// <summary>Beat analysis (null until completed).</summary>
    public BeatGridDto? BeatGrid { get; init; }

    /// <summary>Detail phase (status <c>none</c> when never requested); always present on the wire.</summary>
    public DetailPhaseDto Detail { get; init; } = DetailPhaseDto.None;

    /// <summary>Where the standard separation ran (1.3.0); null before it, for cached results and for older tracks.</summary>
    public JobDeviceDto? Device { get; init; }

    /// <summary>The picture shown (1.4.0); null when there is none (the UI shows a placeholder).</summary>
    public ArtworkDto? Artwork { get; init; }

    /// <summary>Online lookup state and main facts (1.4.0); always present on the wire, <c>none</c> for older tracks.</summary>
    public TrackMetadataDto Metadata { get; init; } = TrackMetadataDto.None;

    /// <summary>Lyrics state (1.6.0); always present on the wire, <c>none</c> for older tracks. The lyrics themselves are only served by <c>GET /api/tracks/{id}/lyrics</c>.</summary>
    public LyricsStatusDto LyricsStatus { get; init; } = LyricsStatusDto.None;

    /// <summary>Where the track came from (1.8.0); null (absent on the wire) for imported tracks.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TrackOriginDto? Origin { get; init; }

    /// <summary>Recorded takes of the track (1.14.0, 0..200), shown in the delete confirmation; absent on the wire when 0.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int TakeCount { get; init; }

    /// <summary>Creation time (UTC).</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Last modification time (UTC).</summary>
    public required DateTimeOffset UpdatedAt { get; init; }
}
