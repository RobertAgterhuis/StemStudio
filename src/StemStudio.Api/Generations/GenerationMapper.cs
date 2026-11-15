using StemStudio.Api.Styles;
using StemStudio.Api.Tracks;
using StemStudio.Contracts;
using StemStudio.Contracts.Generation;
using StemStudio.Domain.Generations;

namespace StemStudio.Api.Generations;

/// <summary>Maps generations to the backend contract (<c>Generation</c>, 1.8.0).</summary>
public static class GenerationMapper
{
    /// <summary>A generation with its takes.</summary>
    /// <param name="generation">The generation.</param>
    /// <returns>The DTO.</returns>
    public static GenerationDto ToDto(Generation generation)
    {
        ArgumentNullException.ThrowIfNull(generation);
        return new GenerationDto
        {
            Id = generation.Id,
            Kind = StyleMapper.Map<GenerationKindDto>(generation.Kind),
            Status = StyleMapper.Map<GenerationStateDto>(generation.Status),
            Progress = generation.Progress,
            Stage = generation.Stage,
            Restyle = generation.Restyle is { } restyle ? ToDto(restyle) : null,
            Takes = generation.Takes.Select(take => ToDto(generation.Id, take)).ToList(),
            Warnings = generation.Warnings,
            Device = TrackMapper.ToDto(generation.Device),
            Error = generation.Error is { } error ? new GenerationErrorDto { Code = StyleMapper.Map<GenerationErrorCodeDto>(error.Code), Message = error.Message } : null,
            CreatedAt = TrackMapper.Utc(generation.CreatedAt),
            UpdatedAt = TrackMapper.Utc(generation.UpdatedAt),
            CompletedAt = generation.CompletedAt is { } completed ? TrackMapper.Utc(completed) : null,
        };
    }

    private static RestyleDetailsDto ToDto(RestyleDetails details) => new()
    {
        SourceTrackId = details.SourceTrackId,
        SourceTrackTitle = details.SourceTrackTitle,
        Range = new SourceRangeDto { StartSeconds = details.Range.StartSeconds, EndSeconds = details.Range.EndSeconds, Mode = StyleMapper.Map<RestyleModeDto>(details.Range.Mode) },
        IncludedStems = details.IncludedStems,
        Style = StyleMapper.ToDto(details.Style),
        Strength = StyleMapper.Map<Contracts.Styles.StrengthDto>(details.Strength),
        Caption = details.Caption,
    };

    private static GenerationTakeDto ToDto(Guid generationId, GenerationTake take) => new()
    {
        Index = take.Index,
        Seed = take.Seed,
        DurationSeconds = take.DurationSeconds,
        AudioUrl = ApiRoutes.TakeAudio(generationId, take.Index),
        LibraryAssetId = take.LibraryAssetId,
        ImportedTrackId = take.ImportedTrackId,
        Model = new GenerationTakeModelDto { Id = take.Model.Id, Revision = take.Model.Revision },
        AiGenerated = take.AiGenerated,
    };
}
