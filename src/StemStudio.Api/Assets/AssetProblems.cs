using Microsoft.AspNetCore.Http.HttpResults;
using StemStudio.Api.LibraryCare.Gate;
using StemStudio.Contracts.Recording;
using StemStudio.Domain;
using StemStudio.Domain.Rendering;

namespace StemStudio.Api.Assets;

/// <summary>Problem responses of the asset endpoints (types and texts from backend 1.5.0).</summary>
public static class AssetProblems
{
    /// <summary>409 <c>track-not-ready</c>.</summary>
    /// <returns>The result.</returns>
    public static IResult TrackNotReady() => Typed(StatusCodes.Status409Conflict, "track-not-ready", "Track not ready", "The track is still being processed; export it when it is completed.");

    /// <summary>409 <c>insufficient-disk-space</c>.</summary>
    /// <returns>The result.</returns>
    public static IResult InsufficientDiskSpace() => Typed(
        StatusCodes.Status409Conflict, "insufficient-disk-space", "Not enough disk space", "Less than 1 GiB is free on the data drive; delete items or free space first.");

    /// <summary>409 <c>source-unavailable</c>.</summary>
    /// <param name="detail">What is missing.</param>
    /// <returns>The result.</returns>
    public static IResult SourceUnavailable(string detail) => Typed(StatusCodes.Status409Conflict, "source-unavailable", "Source unavailable", detail);

    /// <summary>409 <c>asset-not-failed</c>.</summary>
    /// <returns>The result.</returns>
    public static IResult NotFailed() => Typed(StatusCodes.Status409Conflict, "asset-not-failed", "Item is not failed", "Only failed items can be retried.");

    /// <summary>409 <c>managed-by-take</c>: the item is a recorded take (feature 015), owned by the Takes panel.</summary>
    /// <returns>The result.</returns>
    public static IResult ManagedByTake() => GateProblems.Typed(
        StatusCodes.Status409Conflict, RecordingProblemTypes.ManagedByTake, "Managed by its take", "This item is a recorded take; rename, delete or retry it in the Takes panel.");

    /// <summary>422 <c>nothing-to-export</c>.</summary>
    /// <returns>The result.</returns>
    public static IResult NothingToExport() => Typed(StatusCodes.Status422UnprocessableEntity, "nothing-to-export", "Nothing to export", NothingToExportException.Text);

    /// <summary>422 validation problem from domain errors.</summary>
    /// <param name="errors">Domain errors.</param>
    /// <param name="prefix">Path prefix, e.g. <c>mix</c>, or empty.</param>
    /// <returns>The result.</returns>
    public static IResult Invalid(IEnumerable<DomainError> errors, string prefix = "") => ApiProblems.Unprocessable(errors
        .GroupBy(error => prefix.Length == 0 ? error.Field : $"{prefix}.{error.Field}", StringComparer.Ordinal)
        .ToDictionary(group => group.Key, group => group.Select(error => error.Message).ToArray(), StringComparer.Ordinal));

    /// <summary>404 for an unknown item.</summary>
    /// <param name="id">Item ID.</param>
    /// <returns>The result.</returns>
    public static IResult NotFound(Guid id) => ApiProblems.NotFound($"Library item {id} does not exist.");

    private static ProblemHttpResult Typed(int status, string type, string title, string detail) => TypedResults.Problem(detail, statusCode: status, title: title, type: type);
}
