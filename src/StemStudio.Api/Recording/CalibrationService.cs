using StemStudio.Api.Assets;
using StemStudio.Contracts.Recording;
using StemStudio.Domain;
using StemStudio.Domain.Recording;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Recording;

/// <summary>Input-latency calibrations per device (data-model 2.5): list, upsert by trimmed device label, delete.</summary>
/// <param name="calibrations">Calibration repository.</param>
/// <param name="gate">Serialises upserts so one label is never inserted twice.</param>
/// <param name="clock">Clock.</param>
public sealed class CalibrationService(IInputCalibrationRepository calibrations, TakeLock gate, TimeProvider clock)
{
    /// <summary>Every calibration by label.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200.</returns>
    public async Task<IResult> ListAsync(CancellationToken cancellationToken) =>
        TypedResults.Ok((await calibrations.ListAsync(cancellationToken)).Select(TakeMapper.ToDto).ToList());

    /// <summary>Creates or replaces the calibration of a device label.</summary>
    /// <param name="input">The calibration.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200 or 422.</returns>
    public async Task<IResult> UpsertAsync(InputCalibrationInput? input, CancellationToken cancellationToken)
    {
        if (input is null)
        {
            return AssetProblems.Invalid([new DomainError(string.Empty, "Send a calibration.")]);
        }

        var values = new CalibrationValues(
            input.DeviceLabel, input.DeviceIdHint, input.LatencySamples, (CalibrationMethod)(int)input.Method, input.SpreadSamples, input.DetectedClicks, input.OutputLatencySamples);
        if (values.Validate() is { Count: > 0 } errors)
        {
            return AssetProblems.Invalid(errors);
        }

        return await gate.RunAsync(() => StoreAsync(input.DeviceLabel, values, cancellationToken), cancellationToken);
    }

    private async Task<IResult> StoreAsync(string deviceLabel, CalibrationValues values, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var calibration = await calibrations.FindByLabelAsync(InputCalibration.LabelKey(deviceLabel), cancellationToken);
        if (calibration is null)
        {
            calibration = InputCalibration.Create(Guid.NewGuid(), values, now);
            calibrations.Add(calibration);
        }
        else
        {
            calibration.Replace(values, now);
        }

        await calibrations.SaveAsync(cancellationToken);
        return TypedResults.Ok(TakeMapper.ToDto(calibration));
    }

    /// <summary>Deletes a calibration; takes keep their latency and lose the link.</summary>
    /// <param name="calibrationId">The calibration.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>204 or 404.</returns>
    public async Task<IResult> DeleteAsync(Guid calibrationId, CancellationToken cancellationToken)
    {
        var calibration = await calibrations.FindAsync(calibrationId, cancellationToken);
        if (calibration is null)
        {
            return RecordingProblems.CalibrationNotFound(calibrationId);
        }

        await calibrations.RemoveAsync(calibration, cancellationToken);
        await calibrations.SaveAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
