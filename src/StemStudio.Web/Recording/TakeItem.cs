using System.Globalization;
using StemStudio.Contracts.Recording;

namespace StemStudio.Web.Recording;

/// <summary>
/// One take in the panel: a local capture before (or while) it is uploaded, the server take afterwards, or both while
/// the server copy is being saved (ui-test-ids 1.14.0 <c>data-status</c>: <c>local</c>, <c>saving</c>, <c>ready</c>,
/// <c>failed</c>).
/// </summary>
public sealed class TakeItem
{
    private TakeItem(CapturedTake? capture, RecordingTakeDto? take, RecordingTakeUploadDto? upload)
    {
        Capture = capture;
        Take = take;
        Upload = upload;
    }

    /// <summary>The local capture, until it is released.</summary>
    public CapturedTake? Capture { get; private set; }

    /// <summary>The server take, once the upload was accepted.</summary>
    public RecordingTakeDto? Take { get; private set; }

    /// <summary>The upload metadata (kept for "Save again").</summary>
    public RecordingTakeUploadDto? Upload { get; }

    /// <summary>The upload error, or null.</summary>
    public string? Error { get; private set; }

    /// <summary>True while the upload runs.</summary>
    public bool Uploading { get; private set; }

    /// <summary>The engine key of the capture, or null.</summary>
    public string? CaptureKey => Capture?.CaptureKey;

    /// <summary>The id used in test ids and <c>loadTake</c>: the server id, else the capture key.</summary>
    public string Id => Take?.Id.ToString("D", CultureInfo.InvariantCulture) ?? Capture!.CaptureKey;

    /// <summary><c>data-status</c>.</summary>
    public string Status => Take?.Status switch
    {
        TakeStatusDto.Saving => "saving",
        TakeStatusDto.Ready => "ready",
        TakeStatusDto.Failed => "failed",
        _ => "local",
    };

    /// <summary>True when a local capture was refused or failed to upload.</summary>
    public bool UploadFailed => Take is null && Error is not null && !Uploading;

    /// <summary>True while the server converts it.</summary>
    public bool Saving => Take?.Status == TakeStatusDto.Saving || (Take is null && !UploadFailed);

    /// <summary>The session of the take.</summary>
    public Guid SessionId => Take?.SessionId ?? Capture!.SessionId;

    /// <summary>Song sample of the first sample.</summary>
    public long StartSample => Take?.StartSample ?? Capture!.StartSample;

    /// <summary>A new local capture.</summary>
    /// <param name="capture">The capture.</param>
    /// <param name="upload">Its upload metadata.</param>
    /// <returns>The item.</returns>
    public static TakeItem Local(CapturedTake capture, RecordingTakeUploadDto upload) => new(capture, null, upload);

    /// <summary>A take listed by the server.</summary>
    /// <param name="take">The take.</param>
    /// <returns>The item.</returns>
    public static TakeItem Saved(RecordingTakeDto take) => new(null, take, null);

    /// <summary>Marks the upload as running.</summary>
    public void StartUpload() => (Uploading, Error) = (true, null);

    /// <summary>The server accepted the upload or reported a new state.</summary>
    /// <param name="take">The server take.</param>
    public void Apply(RecordingTakeDto take) => (Take, Uploading, Error) = (take, false, null);

    /// <summary>The upload was refused or failed.</summary>
    /// <param name="error">Readable error.</param>
    public void Fail(string error) => (Uploading, Error) = (false, error);

    /// <summary>The local audio was released in the engine.</summary>
    public void ReleaseCapture() => Capture = null;
}
