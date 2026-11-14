using StemStudio.Contracts;
using StemStudio.Contracts.Importing;
using StemStudio.Contracts.Tracks;

namespace StemStudio.Api.Importing;

/// <summary>Maps <c>POST /api/imports</c> and <c>POST /api/imports/upload</c>.</summary>
public static partial class ImportEndpoints
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

    private static async Task<IResult> ImportUrl(ImportUrlRequest? request, ImportService service, ILoggerFactory logs, CancellationToken cancellationToken) =>
        Logged(logs, "url", await service.ImportUrlAsync(request?.Url, cancellationToken));

    private static async Task<IResult> ImportUpload(HttpRequest request, ImportService service, ILoggerFactory logs, CancellationToken cancellationToken) =>
        Logged(logs, "upload", await UploadRequestReader.ReadAsync(
            request, (content, fileName) => service.ImportUploadAsync(content, fileName, cancellationToken), cancellationToken));

    private static IResult Logged(ILoggerFactory logs, string kind, IResult result)
    {
        var logger = logs.CreateLogger(typeof(ImportEndpoints));
        var status = (result as IStatusCodeHttpResult)?.StatusCode;
        LogImport(logger, kind, status);
        return result;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Import ({Kind}) answered {Status}")]
    private static partial void LogImport(ILogger logger, string kind, int? status);
}
