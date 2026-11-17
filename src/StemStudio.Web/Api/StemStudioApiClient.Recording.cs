using System.Net.Http.Headers;
using StemStudio.Contracts;
using StemStudio.Contracts.Recording;

namespace StemStudio.Web.Api;

/// <summary>Take and calibration operations (feature 015, backend contract 1.14.0).</summary>
public sealed partial class StemStudioApiClient
{
    private const string TakeFileName = "capture.wav";

    /// <inheritdoc />
    public Task<RecordingTakeListDto> ListTakesAsync(Guid trackId, CancellationToken cancellationToken = default) =>
        SendAsync<RecordingTakeListDto>(HttpMethod.Get, ApiRoutes.TrackTakesOf(trackId), null, cancellationToken);

    /// <inheritdoc />
    public Task<RecordingTakeDto> CreateTakeAsync(Guid trackId, Stream audio, RecordingTakeUploadDto metadata, CancellationToken cancellationToken = default) =>
        SendAsync<RecordingTakeDto>(HttpMethod.Post, ApiRoutes.TrackTakesOf(trackId), TakeBody(audio, metadata), cancellationToken);

    /// <inheritdoc />
    public Task<RecordingTakeDto> GetTakeAsync(Guid takeId, CancellationToken cancellationToken = default) =>
        SendAsync<RecordingTakeDto>(HttpMethod.Get, ApiRoutes.RecordingTakeOf(takeId), null, cancellationToken);

    /// <inheritdoc />
    public Task<RecordingTakeDto> UpdateTakeAsync(Guid takeId, UpdateTakeRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<RecordingTakeDto>(HttpMethod.Patch, ApiRoutes.RecordingTakeOf(takeId), JsonBody(request), cancellationToken);

    /// <inheritdoc />
    public async Task DeleteTakeAsync(Guid takeId, CancellationToken cancellationToken = default)
    {
        using var response = await SendRawAsync(HttpMethod.Delete, ApiRoutes.RecordingTakeOf(takeId), null, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> DeleteTakesAsync(Guid trackId, IReadOnlyList<Guid> takeIds, CancellationToken cancellationToken = default)
    {
        var body = JsonBody(new DeleteTakesRequest { TakeIds = takeIds });
        var result = await SendAsync<DeleteTakesResultDto>(HttpMethod.Post, ApiRoutes.TrackTakesDeleteOf(trackId), body, cancellationToken);
        return result.Deleted;
    }

    /// <inheritdoc />
    public Task<RecordingTakeDto> ReplaceTakeAudioAsync(Guid takeId, Stream audio, ReplaceTakeAudioMetadataDto metadata, CancellationToken cancellationToken = default) =>
        SendAsync<RecordingTakeDto>(HttpMethod.Put, ApiRoutes.RecordingTakeAudioOf(takeId), TakeBody(audio, metadata), cancellationToken);

    /// <inheritdoc />
    public Task<RecordingTakeDto> RetryTakeAsync(Guid takeId, CancellationToken cancellationToken = default) =>
        SendAsync<RecordingTakeDto>(HttpMethod.Post, ApiRoutes.RecordingTakeRetryOf(takeId), null, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<InputCalibrationDto>> ListCalibrationsAsync(CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<InputCalibrationDto>>(HttpMethod.Get, ApiRoutes.Calibrations, null, cancellationToken);

    /// <inheritdoc />
    public Task<InputCalibrationDto> UpsertCalibrationAsync(InputCalibrationInput input, CancellationToken cancellationToken = default) =>
        SendAsync<InputCalibrationDto>(HttpMethod.Put, ApiRoutes.Calibrations, JsonBody(input), cancellationToken);

    /// <inheritdoc />
    public async Task DeleteCalibrationAsync(Guid calibrationId, CancellationToken cancellationToken = default)
    {
        using var response = await SendRawAsync(HttpMethod.Delete, ApiRoutes.CalibrationOf(calibrationId), null, cancellationToken);
    }

    // The WAV part streams (no buffering in the circuit); the metadata part is JSON (backend 1.14.0 "createRecordingTake").
    private static MultipartFormDataContent TakeBody<T>(Stream audio, T metadata)
    {
        var wav = new StreamContent(audio);
        wav.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        return new MultipartFormDataContent
        {
            { wav, ApiRoutes.RecordingTakeAudioPart, TakeFileName },
            { JsonContent.Create(metadata, options: Json), ApiRoutes.RecordingTakeMetadataPart },
        };
    }
}
