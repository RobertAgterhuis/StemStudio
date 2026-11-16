using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Generation;
using StemStudio.Contracts.Mixes;
using StemStudio.Contracts.Tracks;

namespace StemStudio.Agent.Tests.Fixtures;

/// <summary>Contract DTO samples for provider and job tests.</summary>
public static class Dtos
{
    public static readonly DateTimeOffset At = new(2026, 11, 16, 8, 0, 0, TimeSpan.Zero);

    private static readonly TrackDto SampleTrack = new()
    {
        Id = Guid.Empty,
        Title = "Hero of the Day",
        SourceType = SourceTypeDto.Youtube,
        SourceKey = "yt:abc",
        SourceUrl = "https://www.youtube.com/watch?v=abc",
        Status = TrackStatusDto.Completed,
        Progress = 100,
        DurationSeconds = 260,
        Stems = [new StemDto("vocals", "/api/tracks/x/stems/vocals"), new StemDto("drums", "/api/tracks/x/stems/drums")],
        BeatGrid = new BeatGridDto(120, [0.5], [0.5], 4, 0.9, true),
        CreatedAt = At,
        UpdatedAt = At,
    };

    public static TrackDto Track(Guid? id = null, string title = "Hero of the Day", string? artist = "Metallica") =>
        SampleTrack with { Id = id ?? Guid.NewGuid(), Title = title, Artist = artist };

    public static TrackDto With(this TrackDto track, TrackStatusDto status, int progress = 100) => track with { Status = status, Progress = progress };

    public static TrackDto WithDetail(this TrackDto track, DetailPhaseStatusDto status, int progress = 0, string? error = null) =>
        track with { Detail = new DetailPhaseDto { Status = status, Progress = progress, Error = error, Parents = [] } };

    public static AssetDto Asset(Guid id, AssetStatusDto status, int progress = 0, string? error = null) => new()
    {
        Id = id,
        Kind = AssetKindDto.Audio,
        Name = "Export",
        Status = status,
        Progress = progress,
        Error = error,
        Provenance = new ProvenanceDto { Origin = AssetOriginDto.Export, TrackTitle = "Hero", TrackAvailable = true, Contents = "mix" },
        SizeBytes = 1000,
        CreatedAt = At,
        UpdatedAt = At,
    };

    public static GenerationDto Generation(Guid id, GenerationStateDto status, int progress = 0) => new()
    {
        Id = id,
        Kind = GenerationKindDto.Restyle,
        Status = status,
        Progress = progress,
        CreatedAt = At,
        UpdatedAt = At,
    };

    public static MixSettingsDto Settings(string name = "Practice") => new(
        name,
        0.5,
        [new ChannelSettingsDto("vocals", 1, 0, false, false), new ChannelSettingsDto("drums", 0.5, -0.5, true, false)],
        new MetronomeSettingsDto(false, 0.8, null, 0, 4),
        new LoopRegionDto(10, 20, true));

    public static MixDto Mix(Guid id, Guid trackId, string name = "Practice") => new(id, trackId, Settings(name), At, At);
}
