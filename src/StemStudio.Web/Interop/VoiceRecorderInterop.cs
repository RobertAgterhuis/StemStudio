using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using StemStudio.Contracts;

namespace StemStudio.Web.Interop;

/// <summary>
/// <see cref="IVoiceRecorder"/> over the recorder functions of <c>./js/web-interop.js</c> (feature 010). Scoped per
/// circuit; the module is imported on first use. The recording is streamed from the page with an
/// <see cref="IJSStreamReference"/> (no SignalR message limit).
/// </summary>
/// <param name="js">The circuit's JS runtime.</param>
public sealed class VoiceRecorderInterop(IJSRuntime js) : IVoiceRecorder, IAsyncDisposable
{
    private Task<IJSObjectReference>? _module;

    /// <inheritdoc />
    public async ValueTask<string?> UnavailableReasonAsync() =>
        await (await ModuleAsync()).InvokeAsync<string?>("recorderUnavailableReason");

    /// <inheritdoc />
    public async ValueTask<IVoiceRecording> StartAsync(int maxSeconds)
    {
        try
        {
            return new Recording(await (await ModuleAsync()).InvokeAsync<IJSObjectReference>("startRecording", maxSeconds));
        }
        catch (JSException ex)
        {
            throw new VoiceRecorderException(Readable(ex), ex);
        }
    }

    /// <inheritdoc />
    public async ValueTask<string?> FileUrlAsync(ElementReference input) =>
        await (await ModuleAsync()).InvokeAsync<string?>("fileUrl", input);

    /// <inheritdoc />
    public async ValueTask RevokeUrlAsync(string url) =>
        await (await ModuleAsync()).InvokeVoidAsync("revokeUrl", url);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_module is { IsCompletedSuccessfully: true } module)
        {
            await Recording.SafeAsync(() => module.Result.DisposeAsync());
        }
    }

    /// <summary>The first line of a JS error (the message without the stack).</summary>
    /// <param name="ex">The error.</param>
    /// <returns>The readable message.</returns>
    internal static string Readable(JSException ex) => ex.Message.Split('\n', 2)[0].Trim();

    private Task<IJSObjectReference> ModuleAsync() =>
        _module ??= js.InvokeAsync<IJSObjectReference>("import", WebInterop.ModulePath).AsTask();

    private sealed class Recording(IJSObjectReference session) : IVoiceRecording
    {
        public static async ValueTask SafeAsync(Func<ValueTask> call)
        {
            try
            {
                await call();
            }
            catch (JSDisconnectedException)
            {
                // Circuit gone: the page, its recorder and its microphone no longer exist.
            }
        }

        public async ValueTask<RecordedSample> StopAsync()
        {
            var data = await session.InvokeAsync<IJSStreamReference>("stop");
            await using var source = await data.OpenReadStreamAsync(ContractLimits.MaxVoiceSampleBytes);
            using var bytes = new MemoryStream();
            await source.CopyToAsync(bytes);
            var type = await session.InvokeAsync<string>("mimeType");
            return new RecordedSample(bytes.ToArray(), type, await session.InvokeAsync<string?>("previewUrl"));
        }

        public async ValueTask DisposeAsync()
        {
            await SafeAsync(() => session.InvokeVoidAsync("dispose"));
            await SafeAsync(() => session.DisposeAsync());
        }
    }
}
