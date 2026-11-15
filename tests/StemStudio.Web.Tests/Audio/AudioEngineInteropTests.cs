using System.Text.Json;
using Bunit;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using NSubstitute;
using StemStudio.Contracts;
using StemStudio.Contracts.Mixes;
using StemStudio.Web.Audio;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Audio;

public sealed partial class AudioEngineInteropTests : IAsyncDisposable
{
    private readonly BunitContext _context = new();
    private readonly BunitJSModuleInterop _module;
    private readonly BunitJSInterop _engine;
    private readonly AudioEngineInterop _interop;

    public AudioEngineInteropTests()
    {
        _module = _context.JSInterop.SetupModule("./js/audio-engine.js");
        _engine = _module.SetupModule("createEngine", _ => true);
        _engine.Mode = JSRuntimeMode.Loose;
        var options = Options.Create(new StemStudioWebOptions { PublicApiBaseUrl = new Uri("http://public.test:8081") });
        _interop = new AudioEngineInterop(_context.JSInterop.JSRuntime, options);
    }

    public async ValueTask DisposeAsync()
    {
        await _interop.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Initialize_ImportsModule_AndCreatesEngineWithDotNetReference()
    {
        await _interop.InitializeAsync();

        _context.JSInterop.VerifyInvoke("import").Arguments.ShouldBe(["./js/audio-engine.js"]);
        _module.VerifyInvoke("createEngine").Arguments.Single().ShouldBeOfType<DotNetObjectReference<AudioEngineInterop>>();
        _interop.IsInitialized.ShouldBeTrue();
    }

    [Fact]
    public async Task Initialize_Twice_ReusesEngine()
    {
        await _interop.InitializeAsync();
        await _interop.InitializeAsync();

        _module.VerifyInvoke("createEngine", calledTimes: 1);
    }

    [Fact]
    public async Task LoadTrack_SendsAbsoluteStemUrlsAndBeatGrid_InCamelCase()
    {
        _engine.Setup<double>("loadTrack", _ => true).SetResult(213.04);
        await _interop.InitializeAsync();

        var duration = await _interop.LoadTrackAsync(TestData.Track());

        duration.ShouldBe(213.04);
        var json = JsonSerializer.Serialize(_engine.VerifyInvoke("loadTrack").Arguments.Single(), JsonSerializerOptions.Web);
        json.ShouldContain($"\"url\":\"http://public.test:8081/api/tracks/{TestData.TrackId}/stems/vocals\"");
        json.ShouldContain("\"name\":\"vocals\"");
        json.ShouldContain("\"beatGrid\":{\"bpm\":113.5");
        json.ShouldContain("\"beatsPerBar\":4");
    }

    [Fact]
    public async Task ControlCalls_UseContractMethodNamesAndShapes()
    {
        await _interop.InitializeAsync();

        await _interop.PlayAsync();
        await _interop.PauseAsync();
        await _interop.StopAsync();
        await _interop.SeekAsync(12.5);
        await _interop.SetChannelAsync(new ChannelSettingsDto(StemNames.Guitar, 0.5, -0.25, true, false));
        await _interop.SetMasterAsync(0.9);
        await _interop.SetLoopAsync(new LoopRegionDto(10, 20, true));
        await _interop.SetMetronomeAsync(new MetronomeSettingsDto(true, 0.7, null, 0, 4));
        await _interop.AttachMeterAsync("master", "meter-master");

        _engine.Invocations.Select(i => i.Identifier).ShouldBe(
            ["play", "pause", "stop", "seek", "setChannel", "setMaster", "setLoop", "setMetronome", "attachMeter"]);
        _engine.VerifyInvoke("seek").Arguments.ShouldBe([12.5]);
        var channel = _engine.VerifyInvoke("setChannel").Arguments;
        channel[0].ShouldBe("guitar");
        JsonSerializer.Serialize(channel[1], JsonSerializerOptions.Web).ShouldBe("""{"volume":0.5,"pan":-0.25,"mute":true,"solo":false}""");
        _engine.VerifyInvoke("setMaster").Arguments.ShouldBe([0.9]);
        JsonSerializer.Serialize(_engine.VerifyInvoke("setLoop").Arguments[0], JsonSerializerOptions.Web).ShouldBe("""{"start":10,"end":20,"enabled":true}""");
        JsonSerializer.Serialize(_engine.VerifyInvoke("setMetronome").Arguments[0], JsonSerializerOptions.Web)
            .ShouldBe("""{"enabled":true,"volume":0.7,"bpmOverride":null,"offsetMs":0,"beatsPerBar":4}""");
        _engine.VerifyInvoke("attachMeter").Arguments.ShouldBe(["master", "meter-master"]);
    }

    [Fact]
    public async Task Calls_BeforeInitialize_Throw()
    {
        await Should.ThrowAsync<InvalidOperationException>(() => _interop.PlayAsync());
    }

    [Fact]
    public async Task JSException_IsSurfacedAsAudioEngineException()
    {
        _engine.SetupVoid("setLoop", _ => true).SetException(new JSException("Invalid loop region: end <= start"));
        await _interop.InitializeAsync();

        var exception = await Should.ThrowAsync<AudioEngineException>(() => _interop.SetLoopAsync(new LoopRegionDto(20, 10, true)));

        exception.Message.ShouldBe("Invalid loop region: end <= start");
        exception.InnerException.ShouldBeOfType<JSException>();
    }

    [Fact]
    public async Task LoadTrack_JSException_IsSurfacedAsAudioEngineException()
    {
        _engine.Setup<double>("loadTrack", _ => true).SetException(new JSException("Failed to load stem vocals: HTTP 404"));
        await _interop.InitializeAsync();

        await Should.ThrowAsync<AudioEngineException>(() => _interop.LoadTrackAsync(TestData.Track()));
    }

    [Fact]
    public async Task Initialize_JSException_IsSurfacedAsAudioEngineException()
    {
#pragma warning disable CA2012 // NSubstitute stubs ValueTask-returning members; nothing is consumed twice.
        var js = Substitute.For<IJSRuntime>();
        var module = Substitute.For<IJSObjectReference>();
        js.InvokeAsync<IJSObjectReference>("import", Arg.Any<object?[]?>()).Returns(ValueTask.FromResult(module));
        module.InvokeAsync<IJSObjectReference>("createEngine", Arg.Any<object?[]?>())
            .Returns(ValueTask.FromException<IJSObjectReference>(new JSException("AudioContext not supported")));
#pragma warning restore CA2012
        await using var interop = new AudioEngineInterop(js, Options.Create(new StemStudioWebOptions()));

        await Should.ThrowAsync<AudioEngineException>(() => interop.InitializeAsync());
        interop.IsInitialized.ShouldBeFalse();
    }

    [Fact]
    public async Task Callbacks_RaiseEvents()
    {
        var positions = new List<double>();
        var states = new List<PlaybackState>();
        _interop.PositionChanged += (_, seconds) => positions.Add(seconds);
        _interop.PlaybackStateChanged += (_, state) => states.Add(state);

        _interop.OnPositionChanged(3.5);
        _interop.OnPlaybackStateChanged("playing");
        _interop.OnPlaybackStateChanged("paused");
        _interop.OnPlaybackStateChanged("stopped");
        _interop.OnPlaybackStateChanged("bogus");

        positions.ShouldBe([3.5]);
        states.ShouldBe([PlaybackState.Playing, PlaybackState.Paused, PlaybackState.Stopped]);
    }

    [Fact]
    public async Task Callbacks_WithoutSubscribers_DoNotThrow()
    {
        await using var interop = new AudioEngineInterop(_context.JSInterop.JSRuntime, Options.Create(new StemStudioWebOptions()));

        Should.NotThrow(() => interop.OnPositionChanged(1));
        Should.NotThrow(() => interop.OnPlaybackStateChanged("playing"));
    }

    [Fact]
    public async Task Dispose_DisposesEngine_AndAllowsReinitialize()
    {
        await _interop.InitializeAsync();

        await _interop.DisposeAsync();

        _engine.VerifyInvoke("dispose");
        _interop.IsInitialized.ShouldBeFalse();
        await _interop.InitializeAsync();
        _module.VerifyInvoke("createEngine", calledTimes: 2);
    }

    [Fact]
    public async Task Dispose_IgnoresDisconnectedCircuit()
    {
        _engine.SetupVoid("dispose", _ => true).SetException(new JSDisconnectedException("gone"));
        await _interop.InitializeAsync();

        await Should.NotThrowAsync(async () => await _interop.DisposeAsync());
        _interop.IsInitialized.ShouldBeFalse();
    }

    [Fact]
    public async Task Dispose_IgnoresEngineErrors()
    {
        _engine.SetupVoid("dispose", _ => true).SetException(new JSException("already disposed"));
        await _interop.InitializeAsync();

        await Should.NotThrowAsync(async () => await _interop.DisposeAsync());
    }

    [Fact]
    public async Task Dispose_WithoutInitialize_IsNoOp()
    {
        await Should.NotThrowAsync(async () => await _interop.DisposeAsync());
        _engine.Invocations.ShouldBeEmpty();
    }

    [Fact]
    public void AudioEngineException_StandardConstructors()
    {
        new AudioEngineException().Message.ShouldNotBeNullOrEmpty();
        new AudioEngineException("x").Message.ShouldBe("x");
        new AudioEngineException("x", new InvalidOperationException()).InnerException.ShouldNotBeNull();
    }
}
