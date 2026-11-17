using StemStudio.Contracts.Recording;

namespace StemStudio.Web.Api;

/// <summary>Play-along takes and input calibrations (feature 015, backend contract 1.14.0, tag <c>recording</c>).</summary>
public partial interface IStemStudioApiClient
{
    /// <summary><c>GET /api/tracks/{trackId}/takes</c>: newest first with the total size.</summary>
    /// <param name="trackId">The track.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The takes.</returns>
    Task<RecordingTakeListDto> ListTakesAsync(Guid trackId, CancellationToken cancellationToken = default);

    /// <summary><c>POST /api/tracks/{trackId}/takes</c>: streams the 24-bit WAV and its metadata (multipart).</summary>
    /// <param name="trackId">The track.</param>
    /// <param name="audio">The WAV stream (read once, not disposed here).</param>
    /// <param name="metadata">The take facts.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The take in <c>saving</c> (202); 400, 404, 409, 413, 415, 422 and 503 throw <see cref="ApiException"/>.</returns>
    Task<RecordingTakeDto> CreateTakeAsync(Guid trackId, Stream audio, RecordingTakeUploadDto metadata, CancellationToken cancellationToken = default);

    /// <summary><c>GET /api/takes/{takeId}</c>.</summary>
    /// <param name="takeId">The take.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The take.</returns>
    Task<RecordingTakeDto> GetTakeAsync(Guid takeId, CancellationToken cancellationToken = default);

    /// <summary><c>PATCH /api/takes/{takeId}</c>: rename, star or change the part.</summary>
    /// <param name="takeId">The take.</param>
    /// <param name="request">Only the fields to change.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The updated take.</returns>
    Task<RecordingTakeDto> UpdateTakeAsync(Guid takeId, UpdateTakeRequest request, CancellationToken cancellationToken = default);

    /// <summary><c>DELETE /api/takes/{takeId}</c>.</summary>
    /// <param name="takeId">The take.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A task that completes when the take is deleted.</returns>
    Task DeleteTakeAsync(Guid takeId, CancellationToken cancellationToken = default);

    /// <summary><c>POST /api/tracks/{trackId}/takes/delete</c>: deletes several takes; unknown ids are ignored.</summary>
    /// <param name="trackId">The track.</param>
    /// <param name="takeIds">The takes to delete.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The number deleted.</returns>
    Task<int> DeleteTakesAsync(Guid trackId, IReadOnlyList<Guid> takeIds, CancellationToken cancellationToken = default);

    /// <summary><c>PUT /api/takes/{takeId}/audio</c>: stores the take again with adjusted timing (US7).</summary>
    /// <param name="takeId">The take.</param>
    /// <param name="audio">The re-encoded WAV stream.</param>
    /// <param name="metadata">New span and the applied adjustment.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The take in <c>saving</c>.</returns>
    Task<RecordingTakeDto> ReplaceTakeAudioAsync(Guid takeId, Stream audio, ReplaceTakeAudioMetadataDto metadata, CancellationToken cancellationToken = default);

    /// <summary><c>POST /api/takes/{takeId}/retry</c>: converts a failed take again.</summary>
    /// <param name="takeId">The take.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The take in <c>saving</c>.</returns>
    Task<RecordingTakeDto> RetryTakeAsync(Guid takeId, CancellationToken cancellationToken = default);

    /// <summary><c>GET /api/recording/calibrations</c>.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>One calibration per device label.</returns>
    Task<IReadOnlyList<InputCalibrationDto>> ListCalibrationsAsync(CancellationToken cancellationToken = default);

    /// <summary><c>PUT /api/recording/calibrations</c>: creates or replaces the calibration of a device label.</summary>
    /// <param name="input">The measurement or manual value.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The stored calibration.</returns>
    Task<InputCalibrationDto> UpsertCalibrationAsync(InputCalibrationInput input, CancellationToken cancellationToken = default);

    /// <summary><c>DELETE /api/recording/calibrations/{calibrationId}</c>.</summary>
    /// <param name="calibrationId">The calibration.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A task that completes when it is deleted.</returns>
    Task DeleteCalibrationAsync(Guid calibrationId, CancellationToken cancellationToken = default);
}
