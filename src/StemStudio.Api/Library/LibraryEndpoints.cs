using Microsoft.AspNetCore.Mvc;
using StemStudio.Contracts;
using StemStudio.Contracts.Library;

namespace StemStudio.Api.Library;

/// <summary>Maps the folder view and the live search of the tag <c>library</c> (backend-api 1.22.0).</summary>
public static class LibraryEndpoints
{
    private const string Tag = "library";

    /// <summary>Maps <c>GET /api/library/folders</c> and <c>GET /api/library/folders/{folderId}</c>.</summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The same route builder.</returns>
    public static IEndpointRouteBuilder MapLibraryFolderEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRoutes.LibraryFolders, ([AsParameters] FolderViewRequest request, [FromHeader(Name = "If-None-Match")] string? ifNoneMatch, LibraryBrowseService browse, CancellationToken ct) =>
                browse.FolderAsync(null, request, ifNoneMatch, ct))
            .WithName("getLibraryRoot").WithTags(Tag).Produces<LibraryFolderViewDto>().Produces(StatusCodes.Status304NotModified)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);
        app.MapGet(ApiRoutes.LibraryFolders + "/{folderId}", (string folderId, [AsParameters] FolderViewRequest request, [FromHeader(Name = "If-None-Match")] string? ifNoneMatch, LibraryBrowseService browse, CancellationToken ct) =>
                browse.FolderAsync(folderId, request, ifNoneMatch, ct))
            .WithName("getLibraryFolder").WithTags(Tag).Produces<LibraryFolderViewDto>().Produces(StatusCodes.Status304NotModified)
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);
        return app;
    }
}
