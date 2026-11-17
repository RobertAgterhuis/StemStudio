using StemStudio.Contracts.Library;
using StemStudio.Domain;

namespace StemStudio.Api.Library;

/// <summary>Problem responses of the library folder view and search (backend-api 1.22.0).</summary>
public static class LibraryProblems
{
    /// <summary>404 <c>folder-not-found</c>: a well-formed id without items (any more), or an unknown <c>within</c>.</summary>
    /// <returns>The problem.</returns>
    public static IResult FolderNotFound() => TypedResults.Problem(
        "Its items were moved or deleted.", statusCode: StatusCodes.Status404NotFound, title: "This folder is empty now", type: LibraryProblemTypes.Uri(LibraryProblemTypes.FolderNotFound));

    /// <summary>422 <c>library-query-invalid</c> with the offending parameters.</summary>
    /// <param name="errors">The violated rules.</param>
    /// <returns>The problem.</returns>
    public static IResult QueryInvalid(IEnumerable<DomainError> errors)
    {
        var fields = errors.GroupBy(error => error.Field, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(error => error.Message).ToArray(), StringComparer.Ordinal);
        return TypedResults.Problem(new HttpValidationProblemDetails(fields)
        {
            Type = LibraryProblemTypes.Uri(LibraryProblemTypes.LibraryQueryInvalid),
            Title = "One or more validation errors occurred.",
            Status = StatusCodes.Status422UnprocessableEntity,
        });
    }
}
