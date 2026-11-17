using StemStudio.Contracts;
using StemStudio.Domain;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Library;

/// <summary>
/// Answers the folder view and the live search (feature 022): validates the query, serves the current
/// <see cref="LibrarySnapshot"/> with <c>ETag: "lib-&lt;revision&gt;"</c> and <c>304</c> for a matching
/// <c>If-None-Match</c>, and starts the background filing when items are still unfiled.
/// </summary>
/// <param name="index">The folder index.</param>
/// <param name="library">Data access (revision).</param>
/// <param name="trigger">Starts the background filing.</param>
public sealed class LibraryBrowseService(LibraryIndex index, ILibraryRepository library, ILibraryFilingTrigger trigger)
{
    /// <summary>The root or a folder.</summary>
    /// <param name="folderId">Folder id, or null for the root.</param>
    /// <param name="request">Query string.</param>
    /// <param name="ifNoneMatch">The <c>If-None-Match</c> header.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200, 304, 404 or 422.</returns>
    public async Task<IResult> FolderAsync(string? folderId, FolderViewRequest request, string? ifNoneMatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var errors = new List<DomainError>();
        var query = request.ToQuery(folderId, errors);
        return await AnswerAsync(errors, ifNoneMatch, snapshot => FolderTreeBuilder.View(snapshot, query) is { } view ? TypedResults.Ok(view) : LibraryProblems.FolderNotFound(), cancellationToken);
    }

    /// <summary>The live search.</summary>
    /// <param name="request">Query string.</param>
    /// <param name="ifNoneMatch">The <c>If-None-Match</c> header.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200, 304, 404 (unknown <c>within</c>) or 422.</returns>
    public async Task<IResult> SearchAsync(SearchQueryRequest request, string? ifNoneMatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var errors = new List<DomainError>();
        var search = request.ToRequest(errors);
        return await AnswerAsync(errors, ifNoneMatch, snapshot => LibrarySearch.Run(snapshot, search) is { } result ? TypedResults.Ok(result) : LibraryProblems.FolderNotFound(), cancellationToken);
    }

    /// <summary>The current snapshot; starts the background filing when items are unfiled.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The snapshot.</returns>
    public async Task<LibrarySnapshot> SnapshotAsync(CancellationToken cancellationToken)
    {
        var snapshot = await index.GetAsync(library, cancellationToken);
        if (snapshot.PendingFiling > 0)
        {
            trigger.Trigger();
        }

        return snapshot;
    }

    /// <summary>422 for a query with errors, else the answer on the current snapshot (with <see cref="Respond"/>).</summary>
    /// <param name="errors">The query errors.</param>
    /// <param name="ifNoneMatch">The <c>If-None-Match</c> header.</param>
    /// <param name="answer">Builds the response from the snapshot.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The response.</returns>
    private async Task<IResult> AnswerAsync(List<DomainError> errors, string? ifNoneMatch, Func<LibrarySnapshot, IResult> answer, CancellationToken cancellationToken)
    {
        if (errors.Count > 0)
        {
            return LibraryProblems.QueryInvalid(errors);
        }

        var snapshot = await SnapshotAsync(cancellationToken);
        return Respond(snapshot, ifNoneMatch, () => answer(snapshot));
    }

    /// <summary>304 when the client has the revision, else the result; both with the library ETag.</summary>
    /// <param name="snapshot">The snapshot.</param>
    /// <param name="ifNoneMatch">The <c>If-None-Match</c> header.</param>
    /// <param name="result">Builds the response.</param>
    /// <returns>The response with <c>ETag</c>.</returns>
    public static IResult Respond(LibrarySnapshot snapshot, string? ifNoneMatch, Func<IResult> result)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(result);
        var etag = ApiRoutes.LibraryETag(snapshot.Revision);
        var matches = ifNoneMatch is not null && ifNoneMatch.Split(',').Any(tag => tag.Trim() == etag);
        return new HeaderResult(matches ? TypedResults.StatusCode(StatusCodes.Status304NotModified) : result(), "ETag", etag);
    }
}
