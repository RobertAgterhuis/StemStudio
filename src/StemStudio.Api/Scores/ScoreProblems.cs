using Microsoft.AspNetCore.Http.HttpResults;
using StemStudio.Api.Assets;
using StemStudio.Api.Worker;
using StemStudio.Domain;
using StemStudio.Domain.Scores;
using StemStudio.Domain.Tracks;

namespace StemStudio.Api.Scores;

/// <summary>Problem responses of the score endpoints (types and texts from backend 1.7.0 <c>ScoreConflict</c> and <c>ScoreUnprocessable</c>).</summary>
public static class ScoreProblems
{
    /// <summary>404 for an unknown track, 409 <c>track-not-ready</c> for a track still processing, else null.</summary>
    /// <param name="track">The track, or null.</param>
    /// <param name="trackId">The requested ID.</param>
    /// <returns>The problem or null.</returns>
    public static IResult? TrackProblem(Track? track, Guid trackId) => track switch
    {
        null => Tracks.TrackService.NotFound(trackId),
        { Status: not TrackStatus.Completed } => Typed(StatusCodes.Status409Conflict, "track-not-ready", "Track not ready", "The track is still being processed; create sheet music when it is completed."),
        _ => null,
    };

    /// <summary>409 <c>drums-not-split</c>.</summary>
    /// <returns>The result.</returns>
    public static IResult DrumsNotSplit() =>
        Typed(StatusCodes.Status409Conflict, "drums-not-split", "Drums not split", "Drum notation needs the drum detail stems; split the drums first.");

    /// <summary>409 <c>chords-not-ready</c> (feature 016: <c>includeChords</c> without ready chords).</summary>
    /// <returns>The result.</returns>
    public static IResult ChordsNotReady() =>
        Typed(StatusCodes.Status409Conflict, "chords-not-ready", "Chords not ready", "Chord symbols need the chords of this track; find the chords first.");

    /// <summary>422 <c>tempo-required</c>.</summary>
    /// <returns>The problem.</returns>
    public static IResult TempoRequired() => Typed(
        StatusCodes.Status422UnprocessableEntity, "tempo-required", "Tempo required", "The beat grid of this track is not reliable; enter the tempo, the beats per bar and the first downbeat.");

    /// <summary>422 with one field error.</summary>
    /// <param name="field">The field, e.g. <c>source.channel</c>.</param>
    /// <param name="message">The message.</param>
    /// <returns>The result.</returns>
    public static IResult Field(string field, string message) => ApiProblems.Unprocessable(new Dictionary<string, string[]>(StringComparer.Ordinal) { [field] = [message] });

    /// <summary>The problem of a refused request: 409 for a conflict, 422 for broken rules; null for any other exception.</summary>
    /// <param name="exception">The exception.</param>
    /// <returns>The result or null.</returns>
    public static IResult? For(Exception exception) => exception switch
    {
        ScoreConflictException => DrumsNotSplit(),
        DomainValidationException invalid => AssetProblems.Invalid(invalid.Errors),
        _ => null,
    };

    /// <summary>The answer when the worker refuses or cannot take a transcription: 503 when unreachable, else 409 <c>source-unavailable</c>.</summary>
    /// <param name="exception">The exception.</param>
    /// <returns>The result or null for any other exception.</returns>
    public static IResult? Submission(Exception exception) => exception switch
    {
        WorkerUnavailableException => ApiProblems.Unavailable(),
        WorkerRequestException rejection => AssetProblems.SourceUnavailable(rejection.Detail),
        _ => null,
    };

    private static ProblemHttpResult Typed(int status, string type, string title, string detail) => TypedResults.Problem(detail, statusCode: status, title: title, type: type);
}
