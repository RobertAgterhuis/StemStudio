using StemStudio.Contracts;
using StemStudio.Contracts.Tracks;
using StemStudio.Domain.Tracks.Detail;

namespace StemStudio.Api.Tracks;

/// <summary>Maps the detail phase of a track to the contract DTO (backend 1.2.0 <c>DetailPhase</c>).</summary>
public static class DetailMapper
{
    /// <summary>Maps a detail phase; a track without one gets <see cref="DetailPhaseDto.None"/>.</summary>
    /// <param name="trackId">Track ID (for the detail stem URLs).</param>
    /// <param name="phase">The phase, or null.</param>
    /// <returns>The DTO.</returns>
    public static DetailPhaseDto ToDto(Guid trackId, DetailPhase? phase) => phase is null
        ? DetailPhaseDto.None
        : new DetailPhaseDto
        {
            Status = Enum.Parse<DetailPhaseStatusDto>(phase.Status.ToString()),
            Stage = phase.Stage,
            CurrentParent = phase.CurrentParent,
            Progress = phase.Progress,
            Error = phase.Error,
            Parents = phase.Parents.Select(parent => ToDto(trackId, parent)).ToList(),
            Device = TrackMapper.ToDto(phase.Device),
            UpdatedAt = TrackMapper.Utc(phase.UpdatedAt),
            FilesRemovedAt = phase.FilesRemovedAt is { } removed ? TrackMapper.Utc(removed) : null,
        };

    private static ParentOutcomeDto ToDto(Guid trackId, ParentOutcome parent) => new(
        parent.Parent,
        Enum.Parse<ParentOutcomeKindDto>(parent.Outcome.ToString()),
        parent.Stems.Select(stem => new DetailStemDto(stem.Name, ApiRoutes.DetailStem(trackId, parent.Parent, stem.Name))).ToList());
}
