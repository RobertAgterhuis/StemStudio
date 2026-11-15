using StemStudio.Contracts;
using StemStudio.Contracts.Assets;

namespace StemStudio.Api.Assets;

/// <summary>Maps the library routes of backend 1.5.0 (feature 007).</summary>
public static class AssetEndpoints
{
    /// <summary>Route of one item with a GUID constraint.</summary>
    public const string AssetRoute = ApiRoutes.Assets + "/{assetId:guid}";

    /// <summary>Route of one file of an item.</summary>
    public const string FileRoute = AssetRoute + "/files/{role}";

    /// <summary>Maps the asset endpoints.</summary>
    /// <param name="app">Endpoint builder.</param>
    /// <returns>The builder.</returns>
    public static IEndpointRouteBuilder MapAssetEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost(ApiRoutes.AudioExports, (CreateAudioExportRequest request, ExportService exports, CancellationToken ct) => exports.CreateAsync(request, ct))
            .WithName("createAudioExport").WithTags("assets").Produces<AssetDto>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity).ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        app.MapPost(ApiRoutes.StemPacks, (CreateStemPackRequest request, StemPackService packs, CancellationToken ct) => packs.CreateAsync(request, ct))
            .WithName("createStemPack").WithTags("assets").Produces<StemPackDto>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity).ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        app.MapGet(AssetRoute, (Guid assetId, AssetService assets, CancellationToken ct) => assets.GetAsync(assetId, ct))
            .WithName("getAsset").WithTags("assets").Produces<AssetDto>().ProducesProblem(StatusCodes.Status404NotFound);
        return app.MapLibraryEndpoints().MapFileEndpoints();
    }

    private static IEndpointRouteBuilder MapLibraryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRoutes.Assets, ([AsParameters] AssetListQuery query, AssetLibraryService library, CancellationToken ct) => library.ListAsync(query, ct))
            .WithName("listAssets").WithTags("assets").Produces<AssetListDto>().ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);
        app.MapPatch(AssetRoute, (Guid assetId, RenameAssetRequest? request, AssetService assets, CancellationToken ct) => assets.RenameAsync(assetId, request, ct))
            .WithName("renameAsset").WithTags("assets").Produces<AssetDto>()
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);
        app.MapDelete(AssetRoute, (Guid assetId, AssetLibraryService library, CancellationToken ct) => library.DeleteAsync(assetId, ct))
            .WithName("deleteAsset").WithTags("assets").Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound);
        app.MapPost(AssetRoute + "/retry", (Guid assetId, AssetRetryService retry, CancellationToken ct) => retry.RetryAsync(assetId, ct))
            .WithName("retryAsset").WithTags("assets").Produces<AssetDto>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict).ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        return app;
    }

    private static IEndpointRouteBuilder MapFileEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(FileRoute, ServeFile).WithName("getAssetFile").WithTags("assets")
            .Produces(StatusCodes.Status200OK).Produces(StatusCodes.Status206PartialContent).Produces(StatusCodes.Status304NotModified)
            .ProducesProblem(StatusCodes.Status404NotFound).Produces(StatusCodes.Status416RangeNotSatisfiable);
        app.MapMethods(FileRoute, [HttpMethods.Head], ServeFile).ExcludeFromDescription();
        return app;
    }

    private static Task<IResult> ServeFile([AsParameters] AssetFileRequest request, AssetService assets, CancellationToken ct) =>
        assets.GetFileAsync(request.AssetId, request.Role, request.Inline ?? false, ct);
}

/// <summary>Route and query values of <c>GET /api/assets/{assetId}/files/{role}</c>.</summary>
/// <param name="AssetId">Item ID.</param>
/// <param name="Role">File role.</param>
/// <param name="Inline">True for <c>inline</c>.</param>
public sealed record AssetFileRequest(Guid AssetId, string Role, bool? Inline);
