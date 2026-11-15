using Microsoft.AspNetCore.Http.HttpResults;
using StemStudio.Api.Generator;
using StemStudio.Contracts.Generation;

namespace StemStudio.Api.Generations;

/// <summary>Problem responses of the generation endpoints (backend 1.8.0 <c>GenerationConflict</c>, <c>GeneratorUnavailable</c>).</summary>
public static class GenerationProblems
{
    /// <summary>404 for an unknown generation.</summary>
    /// <param name="id">The id.</param>
    /// <returns>The problem.</returns>
    public static IResult NotFound(Guid id) => ApiProblems.NotFound($"Generation {id} does not exist.");

    /// <summary>409 <c>track-not-ready</c>.</summary>
    /// <returns>The problem.</returns>
    public static IResult TrackNotReady() =>
        Typed(StatusCodes.Status409Conflict, "track-not-ready", "Track not ready", "The track is still being processed; restyle it when it is completed.");

    /// <summary>409 <c>not-installed</c> or <c>gpu-required</c> for a generator that is not ready.</summary>
    /// <param name="readiness">The readiness.</param>
    /// <returns>The problem.</returns>
    public static IResult NotReady(GenerationReadiness readiness)
    {
        ArgumentNullException.ThrowIfNull(readiness);
        return readiness.State == GenerationAvailabilityDto.NotInstalled
            ? Typed(StatusCodes.Status409Conflict, "not-installed", "Generation not installed", "The generator service is not installed. See the help page \"Generation needs a GPU\".")
            : GpuRequired(readiness.Reason ?? "No usable GPU.");
    }

    /// <summary>409 <c>gpu-required</c>.</summary>
    /// <param name="reason">The generator's reason.</param>
    /// <returns>The problem.</returns>
    public static IResult GpuRequired(string reason) => Typed(StatusCodes.Status409Conflict, "gpu-required", "GPU required", reason);

    /// <summary>409 <c>generation-not-completed</c>.</summary>
    /// <returns>The problem.</returns>
    public static IResult NotCompleted() =>
        Typed(StatusCodes.Status409Conflict, "generation-not-completed", "Generation not completed", "Only takes of a completed generation can be imported.");

    /// <summary>409 <c>generation-not-retryable</c>.</summary>
    /// <returns>The problem.</returns>
    public static IResult NotRetryable() =>
        Typed(StatusCodes.Status409Conflict, "generation-not-retryable", "Generation cannot be retried", "Only failed or cancelled generations can be retried.");

    /// <summary>503 <c>generator-unavailable</c>.</summary>
    /// <returns>The problem.</returns>
    public static IResult Unavailable() =>
        Typed(StatusCodes.Status503ServiceUnavailable, "generator-unavailable", "Generator unavailable", "The generator service could not be reached. Try again in a moment.");

    /// <summary>The answer to a refused or failed job submission.</summary>
    /// <param name="exception">The client exception.</param>
    /// <returns>409 for the GPU, 400 for refused input, else 503.</returns>
    public static IResult Submission(Exception exception) => exception switch
    {
        GeneratorRequestException { Code: GeneratorProblemCode.GpuRequired } refused => GpuRequired(refused.Detail),
        GeneratorRequestException { Code: GeneratorProblemCode.InvalidInput or GeneratorProblemCode.SourceTooShort } refused =>
            ApiProblems.Validation("rangeEndSeconds", refused.Detail),
        _ => Unavailable(),
    };

    private static ProblemHttpResult Typed(int status, string type, string title, string detail) =>
        TypedResults.Problem(detail, statusCode: status, title: title, type: type);
}
