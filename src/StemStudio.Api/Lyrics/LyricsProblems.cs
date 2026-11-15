using Microsoft.AspNetCore.Http.HttpResults;
using StemStudio.Api.Worker;
using StemStudio.Domain;
using StemStudio.Domain.Lyrics;

namespace StemStudio.Api.Lyrics;

/// <summary>Results of the lyrics endpoints (backend 1.6.0): every answer carries <c>Cache-Control: no-store</c> (ADR-015 D6).</summary>
public static class LyricsProblems
{
    /// <summary>The <c>Cache-Control</c> value of every lyrics response.</summary>
    public const string NoStore = "no-store";

    /// <summary>Wraps a result with <c>Cache-Control: no-store</c>.</summary>
    /// <param name="result">The result.</param>
    /// <returns>The wrapped result.</returns>
    public static IResult NotStored(IResult result) => new HeaderResult(result, "Cache-Control", NoStore);

    /// <summary>The problem of a refused lyrics transition: 409 for a conflict, 422 for a broken rule.</summary>
    /// <param name="exception">The exception.</param>
    /// <returns>The result, or null for any other exception.</returns>
    public static IResult? For(Exception exception) => exception switch
    {
        LyricsConflictException conflict => Conflict(conflict.Conflict),
        DomainValidationException invalid => Invalid(invalid),
        _ => null,
    };

    /// <summary>The 409 problem of a conflict.</summary>
    /// <param name="conflict">The conflict.</param>
    /// <returns>The result.</returns>
    public static IResult Conflict(LyricsConflict conflict) => conflict switch
    {
        LyricsConflict.Busy => Typed("lyrics-busy", "Lyrics are being processed", "The lyrics are being searched or aligned; try again when it has finished."),
        LyricsConflict.NoText => Typed("lyrics-no-text", "No lyrics text", "There is no lyrics text to align; enter the lyrics first."),
        LyricsConflict.AlreadyTimed => Typed("lyrics-timed", "Lyrics already timed", "These lyrics already have word timing."),
        LyricsConflict.NotAligning => Typed("lyrics-not-aligning", "No alignment running", "No alignment of these lyrics is running."),
        _ => Typed("lyrics-none", "No lyrics", "This track has no lyrics."),
    };

    /// <summary>The answer when the worker cannot take an alignment: 503 when unreachable, else 409 with its detail.</summary>
    /// <param name="exception">The worker exception.</param>
    /// <returns>The result.</returns>
    public static IResult WorkerProblem(Exception exception) => exception is WorkerRequestException rejection
        ? Typed("lyrics-align-rejected", "Alignment rejected", rejection.Detail)
        : ApiProblems.Unavailable();

    /// <summary>The 409 problem while the standard separation is not completed.</summary>
    /// <returns>The result.</returns>
    public static IResult TrackNotReady() => Typed("track-not-ready", "Track not ready", "The track is still being processed; align the lyrics when it is completed.");

    /// <summary>The 422 problem of a broken rule.</summary>
    /// <param name="invalid">The domain error.</param>
    /// <returns>The result.</returns>
    public static IResult Invalid(DomainValidationException invalid)
    {
        ArgumentNullException.ThrowIfNull(invalid);
        return ApiProblems.Unprocessable(invalid.Errors
            .GroupBy(error => error.Field, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(error => error.Message).ToArray(), StringComparer.Ordinal));
    }

    private static ProblemHttpResult Typed(string type, string title, string detail) =>
        TypedResults.Problem(detail, statusCode: StatusCodes.Status409Conflict, title: title, type: type);
}
