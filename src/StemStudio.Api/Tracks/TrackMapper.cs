using StemStudio.Contracts;
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
        return new TrackDto
        {
            Id = track.Id,
            Title = track.Title,
            Artist = track.Artist,
            SourceType = Enum.Parse<SourceTypeDto>(track.SourceType.ToString()),
            SourceKey = track.Key.Value,
            SourceUrl = track.SourceUrl,
            Status = Enum.Parse<TrackStatusDto>(track.Status.ToString()),
            Stage = track.Stage,
            Progress = track.Progress,
            Error = track.Error,
            DurationSeconds = track.DurationSeconds,
            Model = track.Model,
            Stems = track.Stems.Select(stem => new StemDto(stem.Name, ApiRoutes.Stem(track.Id, stem.Name))).ToList(),
            BeatGrid = ToDto(track.BeatGrid),
            CreatedAt = Utc(track.CreatedAt),
            UpdatedAt = Utc(track.UpdatedAt),
        };
    }

    /// <summary>A stored UTC timestamp as an offset-0 <see cref="DateTimeOffset"/>.</summary>
    /// <param name="value">UTC time.</param>
    /// <returns>The timestamp.</returns>
    public static DateTimeOffset Utc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private static BeatGridDto? ToDto(BeatGrid? grid) => grid is null
        ? null
        : new BeatGridDto(grid.Bpm, grid.Beats, grid.Downbeats, grid.BeatsPerBar, grid.Confidence, grid.Reliable);
}
