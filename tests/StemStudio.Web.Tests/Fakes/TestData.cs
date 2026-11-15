using StemStudio.Contracts;
using StemStudio.Contracts.Mixes;
using StemStudio.Contracts.Tracks;

namespace StemStudio.Web.Tests.Fakes;

/// <summary>Contract-shaped sample data (mirrors the examples in backend-api.openapi.yaml).</summary>
public static class TestData
{
    public static readonly Guid TrackId = Guid.Parse("0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a");
    public static readonly Guid MixId = Guid.Parse("6a1f0c9e-2b3d-4e5f-8a9b-0c1d2e3f4a5b");

    public static readonly DateTimeOffset Created = new(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);

    public const string CompletedTrackJson = """
        {
          "id": "0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a", "title": "Never Gonna Give You Up", "artist": "Rick Astley",
          "sourceType": "youtube", "sourceKey": "youtube:dQw4w9WgXcQ", "sourceUrl": "https://youtu.be/dQw4w9WgXcQ",
          "status": "completed", "stage": "Completed", "progress": 100, "error": null, "durationSeconds": 213.04,
          "model": "htdemucs_6s",
          "stems": [ { "name": "vocals", "url": "/api/tracks/0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a/stems/vocals" } ],
          "beatGrid": { "bpm": 113.5, "beats": [0.52, 1.05], "downbeats": [0.52], "beatsPerBar": 4, "confidence": 0.87, "reliable": true },
          "createdAt": "2026-11-14T12:00:00Z", "updatedAt": "2026-11-14T12:06:31Z"
        }
        """;

    /// <summary>Six-stem completed track whose detail phase is explicitly <c>none</c> (contract 1.2.0 wire shape).</summary>
    public const string DetailNoneTrackJson = """
        {
          "id": "0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a", "title": "Never Gonna Give You Up", "artist": "Rick Astley",
          "sourceType": "youtube", "sourceKey": "youtube:dQw4w9WgXcQ", "sourceUrl": "https://youtu.be/dQw4w9WgXcQ",
          "status": "completed", "stage": "Completed", "progress": 100, "error": null, "durationSeconds": 213.04,
          "model": "htdemucs", "stems": [], "beatGrid": null,
          "detail": { "status": "none", "stage": null, "currentParent": null, "progress": 0, "error": null, "parents": [], "updatedAt": null },
          "createdAt": "2026-11-14T12:00:00Z", "updatedAt": "2026-11-14T12:00:20Z"
        }
        """;

    /// <summary>The <c>DetailQueuedTrack</c> example of backend-api.openapi.yaml 1.2.0.</summary>
    public const string DetailQueuedTrackJson = """
        {
          "id": "0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a", "title": "Never Gonna Give You Up", "artist": "Rick Astley",
          "sourceType": "youtube", "sourceKey": "youtube:dQw4w9WgXcQ", "sourceUrl": "https://youtu.be/dQw4w9WgXcQ",
          "status": "completed", "stage": "Completed", "progress": 100, "error": null, "durationSeconds": 213.04,
          "model": "htdemucs", "stems": [], "beatGrid": null,
          "detail": { "status": "queued", "stage": "Queued", "currentParent": null, "progress": 0, "error": null, "parents": [], "updatedAt": "2026-11-15T12:06:31Z" },
          "createdAt": "2026-11-15T12:00:00Z", "updatedAt": "2026-11-15T12:06:31Z"
        }
        """;

    /// <summary>The <c>DetailCompletedTrack</c> example of backend-api.openapi.yaml 1.2.0 (detail block).</summary>
    public const string DetailCompletedTrackJson = """
        {
          "id": "0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a", "title": "Never Gonna Give You Up", "artist": "Rick Astley",
          "sourceType": "youtube", "sourceKey": "youtube:dQw4w9WgXcQ", "sourceUrl": "https://youtu.be/dQw4w9WgXcQ",
          "status": "completed", "stage": "Completed", "progress": 100, "error": null, "durationSeconds": 213.04,
          "model": "htdemucs", "stems": [], "beatGrid": null,
          "detail": {
            "status": "completed", "stage": "Completed", "currentParent": null, "progress": 100, "error": null,
            "parents": [
              { "parent": "vocals", "outcome": "split", "stems": [
                { "name": "lead", "url": "/api/tracks/0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a/detail-stems/vocals/lead" },
                { "name": "backing", "url": "/api/tracks/0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a/detail-stems/vocals/backing" },
                { "name": "rest", "url": "/api/tracks/0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a/detail-stems/vocals/rest" } ] },
              { "parent": "drums", "outcome": "split", "stems": [
                { "name": "kick", "url": "/api/tracks/0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a/detail-stems/drums/kick" },
                { "name": "snare", "url": "/api/tracks/0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a/detail-stems/drums/snare" },
                { "name": "rest", "url": "/api/tracks/0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a/detail-stems/drums/rest" } ] },
              { "parent": "bass", "outcome": "not-supported", "stems": [] },
              { "parent": "other", "outcome": "skipped-silent", "stems": [] }
            ],
            "updatedAt": "2026-11-15T12:31:10Z"
          },
          "createdAt": "2026-11-15T12:00:00Z", "updatedAt": "2026-11-15T12:31:10Z"
        }
        """;

    public const string MixJson = """
        {
          "id": "6a1f0c9e-2b3d-4e5f-8a9b-0c1d2e3f4a5b", "trackId": "0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a",
          "settings": {
            "name": "Guitar practice", "masterVolume": 1,
            "channels": [ { "stem": "guitar", "volume": 0, "pan": 0, "mute": true, "solo": false } ],
            "metronome": { "enabled": true, "volume": 0.7, "bpmOverride": null, "offsetMs": 0, "beatsPerBar": 4 },
            "loop": { "start": 10, "end": 20, "enabled": true }
          },
          "createdAt": "2026-11-14T12:00:00Z", "updatedAt": "2026-11-14T13:00:00Z"
        }
        """;

    public static TrackDto Track(TrackStatusDto status = TrackStatusDto.Completed, string? model = "htdemucs_6s") => new()
    {
        Id = TrackId,
        Title = "Never Gonna Give You Up",
        Artist = "Rick Astley",
        SourceType = SourceTypeDto.Youtube,
        SourceKey = "youtube:dQw4w9WgXcQ",
        SourceUrl = "https://youtu.be/dQw4w9WgXcQ",
        Status = status,
        Stage = status == TrackStatusDto.Completed ? "Completed" : "Separating stems",
        Progress = status == TrackStatusDto.Completed ? 100 : 47,
        DurationSeconds = status == TrackStatusDto.Completed ? 213.04 : null,
        Model = status == TrackStatusDto.Completed ? model : null,
        Stems = status == TrackStatusDto.Completed ? Stems(model) : [],
        BeatGrid = status == TrackStatusDto.Completed ? Grid() : null,
        CreatedAt = Created,
        UpdatedAt = Created,
    };

    public static TrackDto FailedTrack() => Track(TrackStatusDto.Failed) with
    {
        Stage = "Failed",
        Progress = 5,
        Error = "No matching YouTube audio was found for this Spotify track.",
    };

    public static BeatGridDto Grid(bool reliable = true, int beatsPerBar = 4) =>
        new(113.5, [0.52, 1.05, 1.58, 2.11, 2.64], [0.52, 2.64], beatsPerBar, 0.87, reliable);

    public static IReadOnlyList<StemDto> Stems(string? model) =>
        (model == "htdemucs" ? StemNames.FourStem : StemNames.SixStem)
            .Select(name => new StemDto(name, ApiRoutes.Stem(TrackId, name)))
            .ToList();

    public static DetailStemDto DetailStem(string parent, string name) => new(name, ApiRoutes.DetailStem(TrackId, parent, name));

    public static ParentOutcomeDto Split(string parent, params string[] names) =>
        new(parent, ParentOutcomeKindDto.Split, names.Select(n => DetailStem(parent, n)).ToList());

    public static ParentOutcomeDto Outcome(string parent, ParentOutcomeKindDto kind) => new(parent, kind, []);

    public static DetailPhaseDto Phase(DetailPhaseStatusDto status, int progress = 0, string? stage = null, string? error = null) => new()
    {
        Status = status,
        Stage = stage,
        Progress = progress,
        Error = error,
        Parents = [],
        UpdatedAt = Created,
    };

    /// <summary>Completed detail phase: drums and vocals split (stems deliberately out of display order), the rest not split.</summary>
    public static DetailPhaseDto CompletedDetail() => Phase(DetailPhaseStatusDto.Completed, 100, "Completed") with
    {
        Parents =
        [
            Split(StemNames.Drums, "snare", "kick", "rest"),
            Split(StemNames.Vocals, "rest", "lead", "backing"),
            Outcome(StemNames.Bass, ParentOutcomeKindDto.NotSupported),
            Outcome(StemNames.Guitar, ParentOutcomeKindDto.NothingFurther),
            Outcome(StemNames.Piano, ParentOutcomeKindDto.SkippedSilent),
            Outcome(StemNames.Other, ParentOutcomeKindDto.NotReliable),
        ],
    };

    public static TrackDto DetailTrack() => Track() with { Detail = CompletedDetail() };

    public static TrackDto WithDetail(DetailPhaseDto detail) => Track() with { Detail = detail };

    public static MixSettingsDto Settings(string name = "Guitar practice") => new(
        name,
        0.9,
        [new ChannelSettingsDto(StemNames.Guitar, 0, 0, true, false), new ChannelSettingsDto(StemNames.Drums, 0.9, -0.2, false, true)],
        new MetronomeSettingsDto(true, 0.5, 120, 25, 3),
        new LoopRegionDto(10, 20, true));

    public static MixDto Mix(string name = "Guitar practice") =>
        new(MixId, TrackId, Settings(name), Created, Created.AddHours(1));

    public static MixSummaryDto Summary(Guid? id = null, string name = "Guitar practice") =>
        new(id ?? MixId, TrackId, "Never Gonna Give You Up", name, Created.AddHours(1));
}
