using StemStudio.Contracts;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Generations;

/// <summary>Runs a failed or cancelled generation again with the same parameters and new seeds (009 FR-007).</summary>
/// <param name="generations">Repository.</param>
/// <param name="availability">Generator availability.</param>
/// <param name="submitter">Submits the new job.</param>
/// <param name="restyles">Draws the seeds.</param>
/// <param name="clock">Clock.</param>
public sealed class RetryService(
    IGenerationRepository generations, GenerationAvailability availability, GenerationSubmitter submitter, RestyleService restyles, TimeProvider clock)
{
    /// <summary>Creates and submits the retry.</summary>
    /// <param name="id">The failed or cancelled generation.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>202, 404, 409 or 503.</returns>
    public async Task<IResult> RetryAsync(Guid id, CancellationToken cancellationToken)
    {
        var generation = await generations.FindAsync(id, cancellationToken);
        if (generation is null || !generation.CanRetry || generation.SourceTrackId is null)
        {
            return generation is null ? GenerationProblems.NotFound(id) : GenerationProblems.NotRetryable();
        }

        var readiness = await availability.GetAsync(cancellationToken);
        if (!readiness.IsReady)
        {
            return GenerationProblems.NotReady(readiness);
        }

        var retry = generation.Retry(Guid.NewGuid(), restyles.NewSeeds(generation.Seeds.Count), clock.GetUtcNow().UtcDateTime);
        return await submitter.AddAndSubmitAsync(retry, cancellationToken) ?? TypedResults.Accepted(ApiRoutes.Generation(retry.Id), GenerationMapper.ToDto(retry));
    }
}
