using StemStudio.Api.LibraryCare.Gate;
using StemStudio.Contracts.Recording;
using StemStudio.Domain.Recording;

namespace StemStudio.Api.Recording;

/// <summary>Problem responses of the recording endpoints (backend-api 1.14.0; <c>type</c> = <c>https://stemstudio.local/problems/&lt;code&gt;</c>).</summary>
public static class RecordingProblems
{
    /// <summary>404 for an unknown take.</summary>
    /// <param name="takeId">Take id.</param>
    /// <returns>The result.</returns>
    public static IResult TakeNotFound(Guid takeId) => ApiProblems.NotFound($"Take {takeId} does not exist.");

    /// <summary>404 for an unknown calibration.</summary>
    /// <param name="calibrationId">Calibration id.</param>
    /// <returns>The result.</returns>
    public static IResult CalibrationNotFound(Guid calibrationId) => ApiProblems.NotFound($"Calibration {calibrationId} does not exist.");

    /// <summary>409 <c>track-not-ready</c> for recording.</summary>
    /// <returns>The result.</returns>
    public static IResult TrackNotReady() => Typed(StatusCodes.Status409Conflict, "track-not-ready", "Track not ready", "The track is still being processed; record when it is completed.");

    /// <summary>409 <c>insufficient-disk-space</c> for a take.</summary>
    /// <returns>The result.</returns>
    public static IResult InsufficientDiskSpace() => Typed(
        StatusCodes.Status409Conflict, "insufficient-disk-space", "Not enough disk space", "There is not enough free space on the data drive to store this take; delete items or free space first.");

    /// <summary>The problem of a refused upload.</summary>
    /// <param name="refusal">The refusal (not <see cref="TakeRefusal.None"/>).</param>
    /// <returns>The result.</returns>
    public static IResult Refused(TakeRefusal refusal) => refusal switch
    {
        TakeRefusal.LimitReached => Typed(StatusCodes.Status409Conflict, RecordingProblemTypes.TakeLimitReached, "Too many takes", "This song already has 200 takes; delete takes you no longer need."),
        TakeRefusal.Speed => Unprocessable(RecordingProblemTypes.RecordingSpeed, "Recording needs normal speed", "metadata.speed", "Takes can only be recorded at 100 % speed without transpose."),
        TakeRefusal.LengthMismatch => Unprocessable(RecordingProblemTypes.LengthMismatch, "Length mismatch", "metadata.lengthSamples", "The audio is not as long as lengthSamples says."),
        _ => Unprocessable(RecordingProblemTypes.TakeOutsideTrack, "Take outside the song", "metadata.startSample", "The take does not lie inside the song."),
    };

    /// <summary>The problem of a refused WAV (400, 413 or 415).</summary>
    /// <param name="refused">The refusal.</param>
    /// <returns>The result.</returns>
    public static IResult Wav(TakeWavException refused)
    {
        ArgumentNullException.ThrowIfNull(refused);
        return refused.Error switch
        {
            TakeWavError.Unsupported => Typed(StatusCodes.Status415UnsupportedMediaType, RecordingProblemTypes.UnsupportedWavFormat, "Unsupported WAV format", $"{refused.Field}: {refused.Message}"),
            TakeWavError.TooLarge => TooLarge(),
            _ => Invalid(refused.Message),
        };
    }

    /// <summary>400 <c>invalid-wav</c>.</summary>
    /// <param name="detail">What is wrong.</param>
    /// <returns>The result.</returns>
    public static IResult Invalid(string detail) => Typed(StatusCodes.Status400BadRequest, RecordingProblemTypes.InvalidWav, "Not a readable WAV file", detail);

    /// <summary>413 for an upload over the limit.</summary>
    /// <returns>The result.</returns>
    public static IResult TooLarge() => TypedResults.Problem("The take is larger than the upload limit (256 MiB).", statusCode: StatusCodes.Status413PayloadTooLarge, title: "Take too large");

    /// <summary>422 <c>silent-take</c>.</summary>
    /// <returns>The result.</returns>
    public static IResult Silent() => Unprocessable(RecordingProblemTypes.SilentTake, "Nothing was recorded", "audio", "The take is digital silence; check the input and record again.");

    /// <summary>409 <c>take-saving</c>.</summary>
    /// <returns>The result.</returns>
    public static IResult Saving() => Typed(StatusCodes.Status409Conflict, RecordingProblemTypes.TakeSaving, "Take is being saved", "The take is still being saved; try again when it is ready.");

    /// <summary>409 <c>take-not-failed</c>.</summary>
    /// <returns>The result.</returns>
    public static IResult NotFailed() => Typed(StatusCodes.Status409Conflict, RecordingProblemTypes.TakeNotFailed, "Take did not fail", "Only a take that failed to save can be tried again.");

    /// <summary>409 <c>capture-missing</c>.</summary>
    /// <returns>The result.</returns>
    public static IResult CaptureMissing() => Typed(StatusCodes.Status409Conflict, RecordingProblemTypes.CaptureMissing, "Recording is gone", "The recorded audio of this take is no longer stored; record it again.");

    /// <summary>409 <c>take-not-ready</c> (export).</summary>
    /// <returns>The result.</returns>
    public static IResult NotReady() => Typed(StatusCodes.Status409Conflict, RecordingProblemTypes.TakeNotReady, "Take not ready", "The take is still being saved or failed; export it when it is ready.");

    /// <summary>422 <c>take-not-in-track</c> (export).</summary>
    /// <returns>The result.</returns>
    public static IResult NotInTrack() => Unprocessable(RecordingProblemTypes.TakeNotInTrack, "Take of another song", "takeId", "The take belongs to another song.");

    /// <summary>422 <c>take-required</c> (export).</summary>
    /// <returns>The result.</returns>
    public static IResult Required() => Unprocessable(RecordingProblemTypes.TakeRequired, "Take required", "takeId", "The channel 'take' needs a take: send takeId or a mix with a take.");

    /// <summary>A typed 422 with the <c>errors</c> map of <c>ValidationProblemDetails</c> (one field).</summary>
    private static IResult Unprocessable(string code, string title, string field, string detail) => GateProblems.Typed(
        new ProblemText(StatusCodes.Status422UnprocessableEntity, code, title, detail), new Dictionary<string, object?> { ["errors"] = new Dictionary<string, string[]> { [field] = [detail] } });

    private static IResult Typed(int status, string code, string title, string detail) => GateProblems.Typed(status, code, title, detail);
}
