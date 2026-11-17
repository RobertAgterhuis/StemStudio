using StemStudio.Api.LibraryCare.Gate;
using StemStudio.Contracts;
using StemStudio.Contracts.Recording;

namespace StemStudio.Api.Recording;

/// <summary>Maps the routes of the <c>recording</c> tag (backend-api 1.14.0, feature 015).</summary>
public static class TakeEndpoints
{
    private const string Tag = "recording";

    /// <summary>Maps the take and calibration endpoints.</summary>
    /// <param name="app">Endpoint builder.</param>
    /// <returns>The builder.</returns>
    public static IEndpointRouteBuilder MapTakeEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.MapGet(ApiRoutes.TrackTakes, (Guid trackId, TakeService takes, CancellationToken ct) => takes.ListAsync(trackId, ct))
            .WithName("listRecordingTakes").WithTags(Tag).Produces<RecordingTakeListDto>().ProducesProblem(StatusCodes.Status404NotFound);
        app.MapPost(ApiRoutes.TrackTakes, (Guid trackId, HttpRequest request, TakeUploadService uploads, CancellationToken ct) => uploads.CreateAsync(trackId, request, ct))
            .WithName("createRecordingTake").WithTags(Tag).DisableAntiforgery().Produces<RecordingTakeDto>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge).ProducesProblem(StatusCodes.Status415UnsupportedMediaType)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity).ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        app.MapPost(ApiRoutes.TrackTakesDelete, (Guid trackId, DeleteTakesRequest? request, TakeDeletion deletion, CancellationToken ct) => deletion.DeleteManyAsync(trackId, request, ct))
            .WithName("deleteRecordingTakes").RequireGateOpen().WithTags(Tag).Produces<DeleteTakesResultDto>()
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);
        return app.MapOneTake().MapCalibrations();
    }

    private static IEndpointRouteBuilder MapOneTake(this IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRoutes.RecordingTake, (Guid takeId, TakeService takes, CancellationToken ct) => takes.GetAsync(takeId, ct))
            .WithName("getRecordingTake").WithTags(Tag).Produces<RecordingTakeDto>().ProducesProblem(StatusCodes.Status404NotFound);
        app.MapPatch(ApiRoutes.RecordingTake, (Guid takeId, UpdateTakeRequest? request, TakeService takes, CancellationToken ct) => takes.UpdateAsync(takeId, request, ct))
            .WithName("updateRecordingTake").WithTags(Tag).Produces<RecordingTakeDto>()
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);
        app.MapDelete(ApiRoutes.RecordingTake, (Guid takeId, TakeDeletion deletion, CancellationToken ct) => deletion.DeleteAsync(takeId, ct))
            .WithName("deleteRecordingTake").RequireGateOpen().WithTags(Tag).Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound);
        app.MapPut(ApiRoutes.RecordingTakeAudio, (Guid takeId, HttpRequest request, TakeAudioReplacement replacement, CancellationToken ct) => replacement.ReplaceAsync(takeId, request, ct))
            .WithName("replaceRecordingTakeAudio").RequireGateOpen().WithTags(Tag).DisableAntiforgery().Produces<RecordingTakeDto>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge).ProducesProblem(StatusCodes.Status415UnsupportedMediaType)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity).ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        app.MapPost(ApiRoutes.RecordingTakeRetry, (Guid takeId, TakeService takes, CancellationToken ct) => takes.RetryAsync(takeId, ct))
            .WithName("retryRecordingTake").WithTags(Tag).Produces<RecordingTakeDto>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict).ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        return app;
    }

    private static IEndpointRouteBuilder MapCalibrations(this IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRoutes.Calibrations, (CalibrationService calibrations, CancellationToken ct) => calibrations.ListAsync(ct))
            .WithName("listInputCalibrations").WithTags(Tag).Produces<IReadOnlyList<InputCalibrationDto>>();
        app.MapPut(ApiRoutes.Calibrations, (InputCalibrationInput? input, CalibrationService calibrations, CancellationToken ct) => calibrations.UpsertAsync(input, ct))
            .WithName("upsertInputCalibration").WithTags(Tag).Produces<InputCalibrationDto>().ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);
        app.MapDelete(ApiRoutes.Calibration, (Guid calibrationId, CalibrationService calibrations, CancellationToken ct) => calibrations.DeleteAsync(calibrationId, ct))
            .WithName("deleteInputCalibration").WithTags(Tag).Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound);
        return app;
    }
}
