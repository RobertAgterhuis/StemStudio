using StemStudio.Api.Assets;
using StemStudio.Contracts;
using StemStudio.Contracts.Library;

namespace StemStudio.Api.Library;

/// <summary>
/// Maps the six library-info operations of backend-api 1.22.0 (tag <c>library</c>). The writes pass the 013 cross-site
/// guard and the maintenance gate (503 <c>library-maintenance</c>) like every <c>/api</c> write; they carry no backup
/// write-gate mark (research "013 library care": small metadata edits are not gated).
/// </summary>
public static class LibraryInfoEndpoints
{
    /// <summary>Library info of a track.</summary>
    public const string TrackRoute = ApiRoutes.Tracks + "/{trackId:guid}/library-info";

    /// <summary>Library info of an item.</summary>
    public const string ItemRoute = AssetEndpoints.AssetRoute + "/library-info";

    /// <summary>Maps the endpoints.</summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The same route builder.</returns>
    public static IEndpointRouteBuilder MapLibraryInfoEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(TrackRoute, (Guid trackId, LibraryInfoService info, CancellationToken ct) => info.GetTrackAsync(trackId, ct))
            .WithName("getTrackLibraryInfo").WithTags("library").Produces<LibraryInfoStateDto>().ProducesProblem(StatusCodes.Status404NotFound);
        app.MapPut(TrackRoute, (Guid trackId, LibraryInfoRequest request, LibraryInfoService info, CancellationToken ct) => info.SetTrackAsync(trackId, request, ct))
            .WithName("setTrackLibraryInfo").WithTags("library").Produces<LibraryInfoStateDto>()
            .ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound);
        app.MapDelete(TrackRoute, (Guid trackId, LibraryInfoService info, CancellationToken ct) => info.ResetTrackAsync(trackId, ct))
            .WithName("resetTrackLibraryInfo").WithTags("library").Produces<LibraryInfoStateDto>().ProducesProblem(StatusCodes.Status404NotFound);
        return app.MapItemEndpoints();
    }

    private static IEndpointRouteBuilder MapItemEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(ItemRoute, (Guid assetId, LibraryInfoService info, CancellationToken ct) => info.GetItemAsync(assetId, ct))
            .WithName("getAssetLibraryInfo").WithTags("library").Produces<LibraryInfoStateDto>().ProducesProblem(StatusCodes.Status404NotFound);
        app.MapPut(ItemRoute, (Guid assetId, LibraryInfoRequest request, LibraryInfoService info, CancellationToken ct) => info.SetItemAsync(assetId, request, ct))
            .WithName("setAssetLibraryInfo").WithTags("library").Produces<LibraryInfoStateDto>()
            .ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        app.MapDelete(ItemRoute, (Guid assetId, LibraryInfoService info, CancellationToken ct) => info.ResetItemAsync(assetId, ct))
            .WithName("resetAssetLibraryInfo").WithTags("library").Produces<LibraryInfoStateDto>()
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        return app;
    }
}
