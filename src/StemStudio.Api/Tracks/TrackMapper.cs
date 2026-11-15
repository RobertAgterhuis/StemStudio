using StemStudio.Contracts;
using StemStudio.Contracts.Lyrics;
using StemStudio.Contracts.Tracks;
using StemStudio.Domain.Tracks;

namespace StemStudio.Api.Tracks;

/// <summary>Maps <see cref="Track"/> to the contract DTO (ADR-002: mapping lives in Api).</summary>
public static class TrackMapper
{
    /// <summary>Maps a track; stems become relative URLs <c>/api/tracks/{id}/stems/{name}</c> in display order.</summary>
    /// <param name="track">The track.</param>
    /// <returns>The DTO.</returns>
    public static TrackDto ToDto(Track track)
    {
        ArgumentNullException.ThrowIfNull(track);
        return WithFeatures(track, Source(track) with
        {
            Stage = track.Stage,
            Error = track.Error,
            DurationSeconds = track.DurationSeconds,
            Model = track.Model,
            BeatGrid = ToDto(track.BeatGrid),
        });
    }

    private static TrackDto WithFeatures(Track track, TrackDto dto) => dto with
    {
        Detail = DetailMapper.ToDto(track.Id, track.DetailPhase),
        Device = ToDto(track.Device),
        Artwork = ToDto(track.Id, track.Artwork),
        Metadata = ToDto(track.Metadata),
        LyricsStatus = Enum.Parse<LyricsStatusDto>(track.LyricsStatus.ToString()),
    };

    /// <summary>Maps the shown picture to versioned cover and thumbnail URLs (feature 005).</summary>
    /// <param name="trackId">Track ID.</param>
    /// <param name="artwork">The picture, or null.</param>
    /// <returns>The DTO, or null.</returns>
    public static ArtworkDto? ToDto(Guid trackId, TrackArtwork? artwork) => artwork is null
        ? null
        : new ArtworkDto(
            Enum.Parse<ArtworkSourceDto>(artwork.Source.ToString()),
            artwork.Version,
            ApiRoutes.TrackArtwork(trackId, ApiRoutes.ArtworkCover, artwork.Version),
            ApiRoutes.TrackArtwork(trackId, ApiRoutes.ArtworkThumb, artwork.Version));

    /// <summary>Maps the lookup state and its main facts (feature 005).</summary>
    /// <param name="metadata">The lookup state.</param>
    /// <returns>The DTO.</returns>
    public static TrackMetadataDto ToDto(TrackMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        return new TrackMetadataDto
        {
            Status = Enum.Parse<MetadataStatusDto>(metadata.Status.ToString()),
            Reason = metadata.Reason,
            Album = metadata.Album,
            Year = metadata.Year,
            ArtistName = metadata.ArtistName,
            CorrectedByUser = metadata.CorrectedByUser,
        };
    }

    /// <summary>Maps a job device record (feature 003).</summary>
    /// <param name="device">The record, or null.</param>
    /// <returns>The DTO, or null.</returns>
    public static JobDeviceDto? ToDto(JobDevice? device) => device is null
        ? null
        : new JobDeviceDto(Enum.Parse<ProcessingDeviceKindDto>(device.Kind.ToString()), device.Name, device.CpuFallback, device.FallbackReason);

    /// <summary>A stored UTC timestamp as an offset-0 <see cref="DateTimeOffset"/>.</summary>
    /// <param name="value">UTC time.</param>
    /// <returns>The timestamp.</returns>
    public static DateTimeOffset Utc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private static TrackDto Source(Track track) => new()
    {
        Id = track.Id,
        Title = track.Title,
        Artist = track.Artist,
        SourceType = Enum.Parse<SourceTypeDto>(track.SourceType.ToString()),
        SourceKey = track.Key.Value,
        SourceUrl = track.SourceUrl,
        Status = Enum.Parse<TrackStatusDto>(track.Status.ToString()),
        Progress = track.Progress,
        Stems = track.Stems.Select(stem => new StemDto(stem.Name, ApiRoutes.Stem(track.Id, stem.Name))).ToList(),
        CreatedAt = Utc(track.CreatedAt),
        UpdatedAt = Utc(track.UpdatedAt),
    };

    private static BeatGridDto? ToDto(BeatGrid? grid) => grid is null
        ? null
        : new BeatGridDto(grid.Bpm, grid.Beats, grid.Downbeats, grid.BeatsPerBar, grid.Confidence, grid.Reliable);
}
