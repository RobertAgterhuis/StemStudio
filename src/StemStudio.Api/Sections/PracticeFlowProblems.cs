using StemStudio.Contracts;
using StemStudio.Domain;

namespace StemStudio.Api.SongSections;

/// <summary>Problem responses of feature 014 (type <c>https://stemstudio.local/problems/&lt;code&gt;</c>).</summary>
public static class PracticeFlowProblems
{
    /// <summary>409 <c>track-not-ready</c>.</summary>
    /// <returns>The problem.</returns>
    public static IResult TrackNotReady() =>
        Conflict(PracticeFlowProblemTypes.TrackNotReady, "The track is not ready", "Sections can be found when the separation has completed.");

    /// <summary>409 <c>sections-edited</c>.</summary>
    /// <returns>The problem.</returns>
    public static IResult SectionsEdited() =>
        Conflict(PracticeFlowProblemTypes.SectionsEdited, "The sections were edited", "Finding the sections again replaces your edits; confirm to overwrite them.");

    /// <summary>409 <c>detection-running</c>.</summary>
    /// <returns>The problem.</returns>
    public static IResult DetectionRunning() =>
        Conflict(PracticeFlowProblemTypes.DetectionRunning, "The sections are being found", "Wait until the current detection has finished.");

    /// <summary>409 <c>sections-changed</c>.</summary>
    /// <returns>The problem.</returns>
    public static IResult SectionsChanged() =>
        Conflict(PracticeFlowProblemTypes.SectionsChanged, "The sections were changed in another window", "Reload the sections and make your change again.");

    /// <summary>503 <c>worker-unavailable</c>: nothing was queued.</summary>
    /// <returns>The problem.</returns>
    public static IResult WorkerUnavailable() => TypedResults.Problem(
        "The processing service is unavailable. Existing tracks and mixes remain usable.",
        statusCode: StatusCodes.Status503ServiceUnavailable,
        title: "The processing service is not available",
        type: PracticeFlowProblemTypes.BaseUri + "worker-unavailable");

    /// <summary>422 with the field errors of a domain validation.</summary>
    /// <param name="errors">The violations.</param>
    /// <returns>The problem.</returns>
    public static IResult Invalid(IEnumerable<DomainError> errors) => ApiProblems.Unprocessable(errors
        .GroupBy(error => error.Field, StringComparer.Ordinal)
        .ToDictionary(group => group.Key, group => group.Select(error => error.Message).ToArray(), StringComparer.Ordinal));

    /// <summary>A problem of the given code and status.</summary>
    /// <param name="status">HTTP status.</param>
    /// <param name="code">Problem code (last URI segment).</param>
    /// <param name="title">Title.</param>
    /// <param name="detail">Detail.</param>
    /// <returns>The problem.</returns>
    public static IResult Typed(int status, string code, string title, string detail) =>
        TypedResults.Problem(detail, statusCode: status, title: title, type: PracticeFlowProblemTypes.BaseUri + code);

    /// <summary>A 409 of the given code.</summary>
    /// <param name="code">Problem code.</param>
    /// <param name="title">Title.</param>
    /// <param name="detail">Detail.</param>
    /// <returns>The problem.</returns>
    public static IResult Conflict(string code, string title, string detail) => Typed(StatusCodes.Status409Conflict, code, title, detail);
}
