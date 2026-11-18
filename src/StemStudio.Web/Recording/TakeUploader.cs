using Microsoft.JSInterop;
using StemStudio.Contracts.Recording;
using StemStudio.Web.Api;

namespace StemStudio.Web.Recording;

/// <summary>
/// Sends local captures to the backend one after the other (FR-011): the engine's WAV blob is streamed from the browser
/// through the circuit (<see cref="IJSStreamReference"/>, at most <see cref="RecordingLimits.DefaultMaxUploadBytes"/>)
/// straight into the multipart request, never buffered whole. A refused or failed upload keeps the capture for "Save again".
/// </summary>
/// <param name="engine">The recording engine.</param>
/// <param name="api">The backend client.</param>
public sealed class TakeUploader(IRecordingEngine engine, IStemStudioApiClient api) : IDisposable
{
    private readonly SemaphoreSlim _one = new(1, 1);

    /// <summary>Largest WAV read from the browser (the backend's default body limit).</summary>
    public long MaxAllowedSize { get; init; } = RecordingLimits.DefaultMaxUploadBytes;

    /// <summary>Uploads the items in order; each ends with <see cref="TakeItem.Take"/> set or an error.</summary>
    /// <param name="trackId">The track.</param>
    /// <param name="items">Local items with a capture and upload metadata.</param>
    /// <returns>A task that completes when every item was tried.</returns>
    public async Task UploadAsync(Guid trackId, IEnumerable<TakeItem> items)
    {
        foreach (var item in items)
        {
            await _one.WaitAsync();
            try
            {
                await UploadOneAsync(trackId, item);
            }
            finally
            {
                _one.Release();
            }
        }
    }

    /// <inheritdoc />
    public void Dispose() => _one.Dispose();

    private async Task UploadOneAsync(Guid trackId, TakeItem item)
    {
        item.StartUpload();
        try
        {
            item.Apply(await SendAsync(trackId, item));
        }
        catch (ApiException ex)
        {
            item.Fail(ex.Error.UserMessage);
        }
        catch (Exception ex) when (ex is RecordingException or JSException or InvalidOperationException or IOException)
        {
            item.Fail($"The take could not be read from the browser: {ex.Message}");
        }
    }

    private async Task<RecordingTakeDto> SendAsync(Guid trackId, TakeItem item)
    {
        var reference = await engine.CaptureWavAsync(item.CaptureKey!);
        await using (reference)
        {
            await using var stream = await reference.OpenReadStreamAsync(MaxAllowedSize);
            return await api.CreateTakeAsync(trackId, stream, item.Upload!);
        }
    }
}
