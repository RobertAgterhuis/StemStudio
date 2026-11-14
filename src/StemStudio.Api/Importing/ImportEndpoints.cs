using StemStudio.Contracts;
using StemStudio.Contracts.Importing;
using StemStudio.Contracts.Tracks;

namespace StemStudio.Api.Importing;

/// <summary>Maps <c>POST /api/imports</c> and <c>POST /api/imports/upload</c>.</summary>
public static class ImportEndpoints
{
    /// <summary>Maps the import endpoints.</summary>
    /// <param name="app">Endpoint builder.</param>
    /// <returns>The builder.</returns>
    public static IEndpointRouteBuilder MapImportEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost(ApiRoutes.Imports, ImportUrl).WithName("importFromUrl").WithTags("imports")
            .Produces<TrackDto>(StatusCodes.Status200OK).Produces<TrackDto>(StatusCodes.Status202Accepted)
            .ProducesValidationProblem().ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        app.MapPost(ApiRoutes.ImportUpload, ImportUpload).WithName("importFromUpload").WithTags("imports")
            .Produces<TrackDto>(StatusCodes.Status200OK).Produces<TrackDto>(StatusCodes.Status202Accepted)
            .ProducesValidationProblem().ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType).ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        return app;
    }

    private static Task<IResult> ImportUrl(ImportUrlRequest? request, ImportService service, CancellationToken cancellationToken) =>
        service.ImportUrlAsync(request?.Url, cancellationToken);

    private static Task<IResult> ImportUpload(HttpRequest request, ImportService service, CancellationToken cancellationToken) =>
        UploadRequestReader.ReadAsync(request, (content, fileName) => service.ImportUploadAsync(content, fileName, cancellationToken), cancellationToken);
}
