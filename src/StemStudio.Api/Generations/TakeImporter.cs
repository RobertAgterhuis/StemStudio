using StemStudio.Api.Importing;
using StemStudio.Api.Tracks;
using StemStudio.Contracts;
using StemStudio.Contracts.Tracks;
using StemStudio.Domain.Generations;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Generations;

/// <summary>
/// Imports a take as a track (009 FR-009, ADR-018 D10): the existing upload import runs with the take file (source
/// key <c>upload:&lt;sha256&gt;</c>, worker contract unchanged); the track gets the take's title and
/// <c>Origin = generated</c>. Idempotent by the SHA-256 of the file.
/// </summary>
/// <param name="generations">Generation repository.</param>
/// <param name="tracks">Track repository.</param>
/// <param name="imports">The upload import.</param>
/// <param name="storage">Take storage.</param>
/// <param name="clock">Clock.</param>
public sealed class TakeImporter(IGenerationRepository generations, ITrackRepository tracks, ImportService imports, TakeStorage storage, TimeProvider clock)
{
    /// <summary>Imports one take.</summary>
    /// <param name="generationId">The generation.</param>
    /// <param name="index">The take number.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>202 new track, 200 existing track, 404, 409 or 503.</returns>
    public async Task<IResult> ImportAsync(Guid generationId, int index, CancellationToken cancellationToken)
    {
        var generation = await generations.FindAsync(generationId, cancellationToken);
        if (generation is null || (generation.Status == GenerationState.Completed && generation.Take(index) is null))
        {
            return ApiProblems.NotFound($"Generation {generationId} has no take {index}.");
        }

        if (generation.Status != GenerationState.Completed)
        {
            return GenerationProblems.NotCompleted();
        }

        return (await ImportTakeAsync(generation, index, cancellationToken)).Result;
    }

    /// <summary>Imports a take of a completed generation (also used by voice conversions).</summary>
    /// <param name="generation">The completed generation.</param>
    /// <param name="index">An existing take number.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The response and the track id (null when the import failed).</returns>
    public async Task<(IResult Result, Guid? TrackId)> ImportTakeAsync(Generation generation, int index, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(generation);
        var result = await UploadAsync(generation.Take(index)!, cancellationToken);
        return result is IValueHttpResult<TrackDto> { Value: { } dto } ? (await MarkAsync(generation, index, result, cancellationToken), dto.Id) : (result, null);
    }

    private async Task<IResult> UploadAsync(GenerationTake take, CancellationToken cancellationToken)
    {
        await using var file = new FileStream(storage.FullPath(take.FilePath), FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        return await imports.ImportUploadAsync(file, "take.flac", cancellationToken);
    }

    private async Task<IResult> MarkAsync(Generation generation, int index, IResult imported, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var track = (await tracks.FindAsync(((IValueHttpResult<TrackDto>)imported).Value!.Id, cancellationToken))!;
        track.ApplyMetadata(new SourceMetadata(GenerationText.TakeTitle(generation, index), null), now);
        var take = generation.Take(index)!;
        track.MarkGenerated(new TrackOrigin(generation.Id, index, generation.Kind, generation.SourceTrackId)
        {
            Vocal = take.Vocal,
            VoiceProfileName = take.VoiceProfileName,
            LyricsSheetId = generation.Song?.Lyrics.LyricsId,
        }, now);
        generation.MarkImported(index, track.Id, now);
        await tracks.SaveAsync(cancellationToken);
        var dto = TrackMapper.ToDto(track);
        return imported is IStatusCodeHttpResult { StatusCode: StatusCodes.Status202Accepted }
            ? TypedResults.Accepted(ApiRoutes.Track(track.Id), dto)
            : TypedResults.Ok(dto);
    }
}
