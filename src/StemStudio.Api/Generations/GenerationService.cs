using StemStudio.Api.Generator;
using StemStudio.Api.Tracks;
using StemStudio.Domain.Generations;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Generations;

/// <summary>Reads, cancels, retries and deletes generations and serves their takes (009 FR-006, FR-007).</summary>
/// <param name="generations">Repository.</param>
/// <param name="router">Picks the generator or the singer.</param>
/// <param name="storage">Take storage.</param>
/// <param name="library">Library port.</param>
/// <param name="clock">Clock.</param>
public sealed class GenerationService(
    IGenerationRepository generations, GeneratorRouter router, TakeStorage storage, IGeneratedAudioLibrary library, TimeProvider clock)
{
    /// <summary>Generations, newest first.</summary>
    /// <param name="kind">Kind filter (wire name).</param>
    /// <param name="sourceTrackId">Source track filter.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200 or 400.</returns>
    public async Task<IResult> ListAsync(string? kind, string? sourceTrackId, CancellationToken cancellationToken)
    {
        if (!TryKind(kind, out var parsedKind))
        {
            return ApiProblems.Validation("kind", "Use restyle, song or voiceConversion.");
        }

        if (!TryTrack(sourceTrackId, out var parsedTrack))
        {
            return ApiProblems.Validation("sourceTrackId", "Must be a track id.");
        }

        var list = await generations.ListAsync(parsedKind, parsedTrack, cancellationToken);
        return TypedResults.Ok(list.Select(GenerationMapper.ToDto).ToList());
    }

    private static bool TryKind(string? value, out GenerationKind? kind)
    {
        kind = Enum.TryParse<GenerationKind>(value, true, out var parsed) && Enum.IsDefined(parsed) ? parsed : null;
        return value is null || kind is not null;
    }

    private static bool TryTrack(string? value, out Guid? trackId)
    {
        trackId = Guid.TryParse(value, out var parsed) ? parsed : null;
        return value is null || trackId is not null;
    }

    /// <summary>One generation.</summary>
    /// <param name="id">Generation id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200 or 404.</returns>
    public async Task<IResult> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await generations.FindAsync(id, cancellationToken) is { } generation ? TypedResults.Ok(GenerationMapper.ToDto(generation)) : GenerationProblems.NotFound(id);

    /// <summary>Cancels an active generation (the generator is asked best effort); a terminal one is returned unchanged.</summary>
    /// <param name="id">Generation id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200 or 404.</returns>
    public Task<IResult> CancelAsync(Guid id, CancellationToken cancellationToken) => WithAsync(id, generation => CancelAsync(generation, cancellationToken), cancellationToken);

    private async Task<IResult> CancelAsync(Generation generation, CancellationToken cancellationToken)
    {
        if (generation.IsActive)
        {
            await BestEffortAsync(generation.GeneratorJobId, router.For(generation).CancelAsync, cancellationToken);
            generation.Cancel(clock.GetUtcNow().UtcDateTime);
            await generations.SaveAsync(cancellationToken);
        }

        return TypedResults.Ok(GenerationMapper.ToDto(generation));
    }

    /// <summary>Deletes a generation: an active job is removed from the generator, the take files and library items are removed, imported tracks are kept.</summary>
    /// <param name="id">Generation id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>204 or 404.</returns>
    public Task<IResult> DeleteAsync(Guid id, CancellationToken cancellationToken) => WithAsync(id, generation => DeleteAsync(generation, cancellationToken), cancellationToken);

    private async Task<IResult> WithAsync(Guid id, Func<Generation, Task<IResult>> action, CancellationToken cancellationToken) =>
        await generations.FindAsync(id, cancellationToken) is { } generation ? await action(generation) : GenerationProblems.NotFound(id);

    private async Task<IResult> DeleteAsync(Generation generation, CancellationToken cancellationToken)
    {
        if (generation.IsActive)
        {
            await BestEffortAsync(generation.GeneratorJobId, router.For(generation).DeleteAsync, cancellationToken);
        }

        foreach (var assetId in generation.LibraryAssetIds)
        {
            await library.RemoveAsync(assetId, cancellationToken);
        }

        storage.DeleteAll(generation.Id);
        await generations.RemoveAsync(generation, cancellationToken);
        return TypedResults.NoContent();
    }

    /// <summary>Serves the converted vocal of a voice-conversion take.</summary>
    /// <param name="id">Generation id.</param>
    /// <param name="index">Take number.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The file result or 404.</returns>
    public async Task<IResult> GetTakeVocalAsync(Guid id, int index, CancellationToken cancellationToken)
    {
        var path = (await generations.FindAsync(id, cancellationToken))?.Take(index)?.VocalFilePath;
        return StemService.ServeFile(path is null ? null : storage.Find(path), $"Generation {id} has no converted vocal for take {index}.");
    }

    /// <summary>Serves a take of a completed generation with Range/ETag semantics.</summary>
    /// <param name="id">Generation id.</param>
    /// <param name="index">Take number.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The file result or 404.</returns>
    public async Task<IResult> GetTakeAudioAsync(Guid id, int index, CancellationToken cancellationToken)
    {
        var take = (await generations.FindAsync(id, cancellationToken))?.Take(index);
        var file = take is null ? null : storage.Find(take.FilePath);
        return StemService.ServeFile(file, $"Generation {id} has no take {index}.");
    }

    private static async Task BestEffortAsync(string? jobId, Func<string, CancellationToken, Task> call, CancellationToken cancellationToken)
    {
        try
        {
            if (jobId is not null)
            {
                await call(jobId, cancellationToken);
            }
        }
        catch (GeneratorUnavailableException)
        {
            // The job ends with the generator's 24-hour retention; nothing waits for it any more.
        }
    }
}
