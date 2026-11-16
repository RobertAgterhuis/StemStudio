using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using StemStudio.Web.Interop;

namespace StemStudio.Web.Tests.Interop;

/// <summary>The C# side of the recorder in web-interop.js (010 T050, FR-005): MediaRecorder sessions and preview URLs.</summary>
public sealed class VoiceRecorderInteropTests : IAsyncDisposable
{
    private readonly BunitContext _context = new();
    private readonly BunitJSModuleInterop _module;
    private readonly VoiceRecorderInterop _recorder;

    public VoiceRecorderInteropTests()
    {
        _module = _context.JSInterop.SetupModule(WebInterop.ModulePath);
        _recorder = new VoiceRecorderInterop(_context.JSInterop.JSRuntime);
    }

    public async ValueTask DisposeAsync()
    {
        await _recorder.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task UnavailableReason_ComesFromTheModule()
    {
        _module.Setup<string?>("recorderUnavailableReason").SetResult("This browser cannot record from a microphone.");

        (await _recorder.UnavailableReasonAsync()).ShouldBe("This browser cannot record from a microphone.");
    }

    [Fact]
    public async Task ARecording_IsStoppedIntoBytes_WithItsTypeAndPreview()
    {
        var session = _module.SetupModule("startRecording", call => call.Arguments.SequenceEqual(new object?[] { 60 }));
        session.Setup<IJSStreamReference>("stop").SetResult(new BytesReference([1, 2, 3]));
        session.Setup<string>("mimeType").SetResult("audio/webm;codecs=opus");
        session.Setup<string?>("previewUrl").SetResult("blob:preview");
        session.SetupVoid("dispose").SetVoidResult();

        await using var recording = await _recorder.StartAsync(60);
        var sample = await recording.StopAsync();

        (sample.Bytes, sample.ContentType, sample.PreviewUrl).ShouldBe(([1, 2, 3], "audio/webm;codecs=opus", "blob:preview"), new SampleComparer());
    }

    [Fact]
    public async Task AFailedStart_IsAReadableRecorderException()
    {
        await using var recorder = new VoiceRecorderInterop(new ThrowingRuntime(
            new JSException("Microphone permission was denied. Allow the microphone for this page to record.\nError: at startRecording")));

        var ex = await Should.ThrowAsync<VoiceRecorderException>(() => recorder.StartAsync(60).AsTask());

        ex.Message.ShouldBe("Microphone permission was denied. Allow the microphone for this page to record.");
        ex.InnerException.ShouldBeOfType<JSException>();
    }

    /// <summary>A JS runtime whose module throws on every call (bUnit cannot fail an invocation that returns an object reference).</summary>
    private sealed class ThrowingRuntime(Exception error) : IJSRuntime, IJSObjectReference
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            identifier == "import" ? ValueTask.FromResult((TValue)(object)this) : ValueTask.FromException<TValue>(error);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => InvokeAsync<TValue>(identifier, args);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    [Fact]
    public async Task FileUrls_AreCreatedAndRevoked()
    {
        _module.Setup<string?>("fileUrl", _ => true).SetResult("blob:file");
        _module.SetupVoid("revokeUrl", _ => true).SetVoidResult();

        (await _recorder.FileUrlAsync(default(ElementReference))).ShouldBe("blob:file");
        await _recorder.RevokeUrlAsync("blob:file");

        _module.VerifyInvoke("revokeUrl").Arguments.ShouldBe(["blob:file"]);
    }

    [Fact]
    public async Task DisposingASession_SurvivesALostCircuit()
    {
        var session = _module.SetupModule("startRecording", _ => true);
        session.SetupVoid("dispose").SetException(new JSDisconnectedException("gone"));

        var recording = await _recorder.StartAsync(60);

        await Should.NotThrowAsync(() => recording.DisposeAsync().AsTask());
    }

    private sealed class BytesReference(byte[] bytes) : IJSStreamReference
    {
        public long Length => bytes.Length;

        public ValueTask<Stream> OpenReadStreamAsync(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<Stream>(new MemoryStream(bytes));

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class SampleComparer : IEqualityComparer<(byte[], string, string?)>
    {
        public bool Equals((byte[], string, string?) x, (byte[], string, string?) y) =>
            x.Item1.SequenceEqual(y.Item1) && x.Item2 == y.Item2 && x.Item3 == y.Item3;

        public int GetHashCode((byte[], string, string?) obj) => obj.Item2.GetHashCode(StringComparison.Ordinal);
    }
}
