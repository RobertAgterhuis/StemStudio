using System.Globalization;

namespace StemStudio.Contracts;

/// <summary>Routes of play-along recording (backend-api 1.14.0, feature 015, tag <c>recording</c>).</summary>
public static partial class ApiRoutes
{
    /// <summary>Route template: GET (list) / POST (upload) the takes of a track.</summary>
    public const string TrackTakes = "/api/tracks/{trackId}/takes";

    /// <summary>Route template: POST deletes several takes of a track.</summary>
    public const string TrackTakesDelete = TrackTakes + "/delete";

    /// <summary>Route template: GET / PATCH / DELETE one take.</summary>
    public const string RecordingTake = "/api/takes/{takeId}";

    /// <summary>Route template: PUT stores the take again with adjusted timing.</summary>
    public const string RecordingTakeAudio = RecordingTake + "/audio";

    /// <summary>Route template: POST retries saving a failed take.</summary>
    public const string RecordingTakeRetry = RecordingTake + "/retry";

    /// <summary>GET (list) / PUT (upsert by device label) input-latency calibrations.</summary>
    public const string Calibrations = "/api/recording/calibrations";

    /// <summary>Route template: DELETE one calibration.</summary>
    public const string Calibration = Calibrations + "/{calibrationId}";

    /// <summary>Multipart part name of the WAV in the two upload endpoints.</summary>
    public const string RecordingTakeAudioPart = "audio";

    /// <summary>Multipart part name of the JSON metadata in the two upload endpoints.</summary>
    public const string RecordingTakeMetadataPart = "metadata";

    /// <summary>The takes of a track.</summary>
    /// <param name="trackId">Track id.</param>
    /// <returns>The path.</returns>
    public static string TrackTakesOf(Guid trackId) => string.Create(CultureInfo.InvariantCulture, $"/api/tracks/{trackId:D}/takes");

    /// <summary>Batch delete of a track's takes.</summary>
    /// <param name="trackId">Track id.</param>
    /// <returns>The path.</returns>
    public static string TrackTakesDeleteOf(Guid trackId) => TrackTakesOf(trackId) + "/delete";

    /// <summary>One take.</summary>
    /// <param name="takeId">Take id.</param>
    /// <returns>The path.</returns>
    public static string RecordingTakeOf(Guid takeId) => string.Create(CultureInfo.InvariantCulture, $"/api/takes/{takeId:D}");

    /// <summary>The replace-audio endpoint of a take.</summary>
    /// <param name="takeId">Take id.</param>
    /// <returns>The path.</returns>
    public static string RecordingTakeAudioOf(Guid takeId) => RecordingTakeOf(takeId) + "/audio";

    /// <summary>The retry endpoint of a take.</summary>
    /// <param name="takeId">Take id.</param>
    /// <returns>The path.</returns>
    public static string RecordingTakeRetryOf(Guid takeId) => RecordingTakeOf(takeId) + "/retry";

    /// <summary>One calibration.</summary>
    /// <param name="calibrationId">Calibration id.</param>
    /// <returns>The path.</returns>
    public static string CalibrationOf(Guid calibrationId) => string.Create(CultureInfo.InvariantCulture, $"{Calibrations}/{calibrationId:D}");
}
