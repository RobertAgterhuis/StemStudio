using StemStudio.Contracts.Library;
using StemStudio.Domain;

namespace StemStudio.Api.Library;

/// <summary>Problem responses of the library-info operations (backend-api 1.22.0, tag <c>library</c>).</summary>
public static class LibraryInfoProblems
{
    /// <summary>404 for an unknown track.</summary>
    /// <param name="trackId">Track ID.</param>
    /// <returns>The problem.</returns>
    public static IResult TrackNotFound(Guid trackId) => ApiProblems.NotFound($"Track {trackId} does not exist.");

    /// <summary>404 for an unknown library item.</summary>
    /// <param name="assetId">Item ID.</param>
    /// <returns>The problem.</returns>
    public static IResult ItemNotFound(Guid assetId) => ApiProblems.NotFound($"Library item {assetId} does not exist.");

    /// <summary>409 <c>not-fileable</c>: recorded takes (015) live with their track and are never filed.</summary>
    /// <returns>The problem.</returns>
    public static IResult NotFileable() => TypedResults.Problem(
        "Recorded takes stay with their track.",
        statusCode: StatusCodes.Status409Conflict,
        title: "Recorded takes are not filed",
        type: LibraryProblemTypes.Uri(LibraryProblemTypes.NotFileable));

    /// <summary>400 with field messages (<c>artist</c>, <c>album</c>, <c>song</c>).</summary>
    /// <param name="invalid">The validation failure.</param>
    /// <returns>The problem.</returns>
    public static IResult Invalid(DomainValidationException invalid)
    {
        ArgumentNullException.ThrowIfNull(invalid);
        return ApiProblems.Validation(invalid.Errors, string.Empty);
    }
}
