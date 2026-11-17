using System.Text.Json;
using Bunit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using NSubstitute;
using StemStudio.Contracts.Recording;
using StemStudio.Web.Audio;
using StemStudio.Web.Recording;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Recording;

/// <summary>015 T026: JS calls and argument shapes of every 2.5.0 recording method, callbacks and error codes.</summary>
public sealed class RecordingInteropTests : IAsyncDisposable
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private readonly BunitContext _context = new();
    private readonly BunitJSInterop _engine;
    private readonly AudioEngineInterop _audio;
    private readonly RecordingInterop _recording;

    public RecordingInteropTests()
    {
        var module = _context.JSInterop.SetupModule("./js/audio-engine.js");
        _engine = module.SetupModule("createEngine", _ => true);
        _engine.Mode = JSRuntimeMode.Loose;
        var options = Options.Create(new StemStudioWebOptions { PublicApiBaseUrl = new Uri("http://public.test:8081") });
        _audio = new AudioEngineInterop(_context.JSInterop.JSRuntime, options);
        _recording = new RecordingInterop(_audio);
    }

    public async ValueTask DisposeAsync()
    {
        await _audio.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task BeforeInitialize_CallsThrow_AndReleaseAndUnloadAreNoOps()
    {
        _recording.IsAvailable.ShouldBeFalse();

        await Should.ThrowAsync<InvalidOperationException>(() => _recording.GetSupportAsync());
        await Should.ThrowAsync<InvalidOperationException>(() => _recording.CloseInputAsync());
        await _recording.ReleaseCaptureAsync("c1");
        await _recording.UnloadTakeAsync();
        _engine.Invocations.ShouldBeEmpty();
    }

    [Fact]
    public async Task RecordingSupport_ParsesReasonAndPermission()
    {
        _engine.Setup<RecordingSupport>("recordingSupport", _ => true)
            .SetResult(new RecordingSupport(false, "insecureContext", "Open StemStudio on this computer", MicrophoneAccess.Unknown));
        await _audio.InitializeAsync();

        var support = await _recording.GetSupportAsync();

        _recording.IsAvailable.ShouldBeTrue();
        support.Supported.ShouldBeFalse();
        support.ReasonCode.ShouldBe("insecureContext");
        _engine.VerifyInvoke("recordingSupport").Arguments.ShouldBeEmpty();
    }

    [Fact]
    public async Task ListInputDevices_ReturnsTheDevices()
    {
        _engine.Setup<InputDevice[]>("listInputDevices", _ => true).SetResult([new("d1", "USB Mic", "g1")]);
        await _audio.InitializeAsync();

        var devices = await _recording.ListInputDevicesAsync();

        devices.Single().Label.ShouldBe("USB Mic");
    }

    [Fact]
    public async Task OpenInput_SendsDeviceAndKebabMode()
    {
        _engine.Setup<InputInfo>("openInput", _ => true)
            .SetResult(new InputInfo("d1", "USB Mic", 2, InputChannelModeDto.Stereo, 48_000, 441, 300));
        await _audio.InitializeAsync();

        var info = await _recording.OpenInputAsync(new OpenInputRequest("d1", InputChannelModeDto.Stereo));

        info.EstimatedLatencySamples.ShouldBe(441);
        Json(_engine.VerifyInvoke("openInput").Arguments[0]).ShouldBe("""{"deviceId":"d1","mode":"stereo"}""");
    }

    [Theory]
    [InlineData("permissionDenied: Allow the microphone in the address bar.", RecordingErrorCode.PermissionDenied, "Allow the microphone in the address bar.")]
    [InlineData("notFound: No input device.", RecordingErrorCode.NotFound, "No input device.")]
    [InlineData("busy: Another program uses the device.", RecordingErrorCode.Busy, "Another program uses the device.")]
    [InlineData("modeUnavailable: The device has one channel.", RecordingErrorCode.ModeUnavailable, "The device has one channel.")]
    [InlineData("unsupported: The device runs at 48 kHz.", RecordingErrorCode.Unsupported, "The device runs at 48 kHz.")]
    [InlineData("recordingSpeed: Set the speed to 100 %.", RecordingErrorCode.RecordingSpeed, "Set the speed to 100 %.")]
    [InlineData("speedTrainer: Switch the speed trainer off.", RecordingErrorCode.SpeedTrainer, "Switch the speed trainer off.")]
    [InlineData("Stop the recording first", RecordingErrorCode.Other, "Stop the recording first")]
    public async Task EngineErrors_ParseTheCodePrefix(string message, RecordingErrorCode code, string sentence)
    {
        _engine.Setup<InputInfo>("openInput", _ => true).SetException(new JSException(message + "\n    at openInput (engine.js:1)"));
        await _audio.InitializeAsync();

        var error = await Should.ThrowAsync<RecordingException>(() => _recording.OpenInputAsync(new OpenInputRequest(null, InputChannelModeDto.Mono1)));

        error.Code.ShouldBe(code);
        error.Message.ShouldBe(sentence);
        error.InnerException.ShouldBeOfType<JSException>();
    }

    [Fact]
    public async Task VoidCalls_WrapJsErrors()
    {
        _engine.SetupVoid("closeInput", _ => true).SetException(new JSException("Stop the recording first"));
        await _audio.InitializeAsync();

        var error = await Should.ThrowAsync<RecordingException>(() => _recording.CloseInputAsync());

        error.Code.ShouldBe(RecordingErrorCode.Other);
    }

    [Fact]
    public async Task VoidMethods_SendTheirArguments()
    {
        await _audio.InitializeAsync();

        await _recording.CloseInputAsync();
        await _recording.SetMonitoringAsync(new MonitoringSettings(true, 0.5));
        await _recording.ReleaseCaptureAsync("c1");
        await _recording.UnloadTakeAsync();
        await _recording.SetTakeAdjustmentAsync(-441);
        await _recording.SetTakeCompareAsync(TakeCompareMode.Original);

        _engine.VerifyInvoke("closeInput").Arguments.ShouldBeEmpty();
        Json(_engine.VerifyInvoke("setMonitoring").Arguments[0]).ShouldBe("""{"enabled":true,"volume":0.5}""");
        _engine.VerifyInvoke("releaseCapture").Arguments.ShouldBe(["c1"]);
        _engine.VerifyInvoke("unloadTake").Arguments.ShouldBeEmpty();
        _engine.VerifyInvoke("setTakeAdjustment").Arguments.ShouldBe([-441]);
        Json(_engine.VerifyInvoke("setTakeCompare").Arguments[0]).ShouldBe("\"original\"");
    }

    [Fact]
    public async Task CalibrateLatency_SendsOptions_AndDefaultsToEmptyOptions()
    {
        var result = new CalibrationResult(true, 1102, 25.0, 8, 3, 300, -12.5, null, []);
        _engine.Setup<CalibrationResult>("calibrateLatency", _ => true).SetResult(result);
        await _audio.InitializeAsync();

        (await _recording.CalibrateLatencyAsync()).LatencySamples.ShouldBe(1102);
        await _recording.CalibrateLatencyAsync(new CalibrationOptions(Clicks: 8, Seed: 7));

        var calls = _engine.Invocations.Where(i => i.Identifier == "calibrateLatency").ToList();
        Json(calls[0].Arguments[0]).ShouldBe("""{"clicks":null,"seed":null}""");
        Json(calls[1].Arguments[0]).ShouldBe("""{"clicks":8,"seed":7}""");
    }

    [Fact]
    public async Task StartRecording_SendsTheRecordOptionsInContractNames()
    {
        await _audio.InitializeAsync();

        await _recording.StartRecordingAsync(new RecordOptions
        {
            LatencySamples = 1102,
            LatencyMethod = LatencyMethodDto.Loopback,
            Part = RecordingPartDto.Bass,
            MuteOriginal = true,
            CountInBars = 1,
        });

        Json(_engine.VerifyInvoke("startRecording").Arguments[0]).ShouldBe(
            """{"latencySamples":1102,"latencyMethod":"loopback","part":"bass","muteOriginal":true,"countInBars":1}""");
    }

    [Fact]
    public async Task StopRecording_ReturnsCapturedTakesParsedFromEngineJson()
    {
        _engine.Setup<CapturedTake[]>("stopRecording", _ => true).SetResult([Take()]);
        await _audio.InitializeAsync();

        var takes = await _recording.StopRecordingAsync();

        takes.Single().CaptureKey.ShouldBe("c1");
    }

    [Fact]
    public void CapturedTake_DeserializesTheEngineShape()
    {
        const string json = """
            {"captureKey":"c1","sessionId":"0b9f6c8e-58a5-4d2b-9f0e-0d7f1e2a3b4c","passIndex":2,"partial":true,
             "startSample":44100,"lengthSamples":88200,"channels":1,"inputMode":"mono-2","latencySamples":1200,
             "latencyMethod":"estimated","peakDbfs":-6.5,"clippedSamples":0,"dropouts":1,"recordedAt":"2026-11-17T10:00:00Z"}
            """;

        var take = JsonSerializer.Deserialize<CapturedTake>(json, Web)!;

        take.SessionId.ShouldBe(Guid.Parse("0b9f6c8e-58a5-4d2b-9f0e-0d7f1e2a3b4c"));
        take.InputMode.ShouldBe(InputChannelModeDto.Mono2);
        take.LatencyMethod.ShouldBe(LatencyMethodDto.Estimated);
        take.PassIndex.ShouldBe(2);
        take.RecordedAt.ShouldBe(new DateTimeOffset(2026, 11, 17, 10, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void RecordingSupport_DeserializesPermission()
    {
        var support = JsonSerializer.Deserialize<RecordingSupport>(
            """{"supported":true,"reasonCode":null,"reason":null,"permission":"prompt"}""", Web)!;

        support.Permission.ShouldBe(MicrophoneAccess.Prompt);
    }

    [Fact]
    public async Task CaptureWav_ReturnsTheStreamReference()
    {
        var stream = Substitute.For<IJSStreamReference>();
        _engine.Setup<IJSStreamReference>("captureWav", _ => true).SetResult(stream);
        await _audio.InitializeAsync();

        (await _recording.CaptureWavAsync("c1")).ShouldBeSameAs(stream);
        _engine.VerifyInvoke("captureWav").Arguments.ShouldBe(["c1"]);
    }

    [Fact]
    public async Task LoadTake_FromCapture_SendsOnlyTheCaptureKey()
    {
        await _audio.InitializeAsync();

        await _recording.LoadTakeAsync(new LoadTakeRequest("c1", TakeSource.FromCapture("c1"), RecordingPartDto.Vocals));

        Json(_engine.VerifyInvoke("loadTake").Arguments[0]).ShouldBe(
            """{"takeId":"c1","source":{"captureKey":"c1"},"part":"vocals","adjustmentSamples":0}""");
    }

    [Fact]
    public async Task LoadTake_FromUrl_SendsTheSavedSpan()
    {
        await _audio.InitializeAsync();

        await _recording.LoadTakeAsync(new LoadTakeRequest(
            "t1", TakeSource.FromUrl("http://api/x.flac", 100, 200, 2), RecordingPartDto.None, -441));

        Json(_engine.VerifyInvoke("loadTake").Arguments[0]).ShouldBe(
            """{"takeId":"t1","source":{"url":"http://api/x.flac","startSample":100,"lengthSamples":200,"channels":2},"part":"none","adjustmentSamples":-441}""");
    }

    [Fact]
    public void Callbacks_RouteToTheHub()
    {
        var states = new List<RecordingState>();
        var progress = new List<RecordingProgress>();
        IReadOnlyList<CapturedTake>? captured = null;
        string? lost = null;
        var changed = 0;
        _recording.Callbacks.ShouldBeSameAs(_audio.RecordingCallbacks);
        _recording.Callbacks.StateChanged += (_, s) => states.Add(s);
        _recording.Callbacks.ProgressChanged += (_, p) => progress.Add(p);
        _recording.Callbacks.TakesCaptured += (_, t) => captured = t;
        _recording.Callbacks.InputLost += (_, r) => lost = r;
        _recording.Callbacks.InputDevicesChanged += (_, _) => changed++;

        _audio.OnRecordingStateChanged("countIn");
        _audio.OnRecordingStateChanged("bogus");
        _audio.OnRecordingStateChanged("finishing");
        _audio.OnRecordingProgress(new RecordingProgress(1.5, 2, null));
        _audio.OnTakesCaptured([Take()]);
        _audio.OnInputLost("ended");
        _audio.OnInputDevicesChanged();

        states.ShouldBe([RecordingState.CountIn, RecordingState.Finishing]);
        progress.Single().PassIndex.ShouldBe(2);
        captured!.Single().CaptureKey.ShouldBe("c1");
        lost.ShouldBe("ended");
        changed.ShouldBe(1);
    }

    [Fact]
    public void Callbacks_WithoutSubscribers_DoNotThrow()
    {
        var hub = new RecordingCallbacks();

        hub.RaiseStateChanged("open");
        hub.RaiseProgress(new RecordingProgress(0, null, 3));
        hub.RaiseTakesCaptured([]);
        hub.RaiseInputLost("x");
        hub.RaiseInputDevicesChanged();

        RecordingCallbacks.ParseState("RECORDING").ShouldBe(RecordingState.Recording);
        RecordingCallbacks.ParseState(null).ShouldBeNull();
        RecordingCallbacks.ParseState("7").ShouldBeNull();
    }

    [Fact]
    public void RecordingException_DefaultConstructors()
    {
        new RecordingException().Code.ShouldBe(RecordingErrorCode.Other);
        new RecordingException("x").Message.ShouldBe("x");
        new RecordingException("x", new InvalidOperationException()).InnerException.ShouldBeOfType<InvalidOperationException>();
    }

    [Fact]
    public void EngineOptions_LoopbackOnlyWhenConfigured()
    {
        new RecordingEngineOptions(Options.Create(new RecordingUiOptions())).Options.ShouldBeNull();

        var options = new RecordingEngineOptions(Options.Create(new RecordingUiOptions { TestLoopback = true })).Options;

        Json(options).ShouldBe("""{"testInput":"loopback"}""");
    }

    [Fact]
    public async Task Registration_ResolvesTheRecordingServicesOnTheSharedEngine()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["StemStudio:Recording:TestLoopback"] = "true" })
            .Build();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton(_context.JSInterop.JSRuntime);
        services.AddSingleton(Options.Create(new StemStudioWebOptions { PublicApiBaseUrl = new Uri("http://api.test") }));
        services.AddScoped<IAudioEngine, AudioEngineInterop>();
        services.AddRecordingUi();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var audio = (AudioEngineInterop)scope.ServiceProvider.GetRequiredService<IAudioEngine>();

        scope.ServiceProvider.GetRequiredService<IRecordingEngine>().Callbacks.ShouldBeSameAs(audio.RecordingCallbacks);
        scope.ServiceProvider.GetRequiredService<RecordingPreferences>().ShouldNotBeNull();
        Json(scope.ServiceProvider.GetRequiredService<IEngineOptionsSource>().Options).ShouldBe("""{"testInput":"loopback"}""");
    }

    [Fact]
    public void RecordingMethods_ExistInTheEngineContract()
    {
        var typesTs = File.ReadAllText(Path.Combine(RepoPaths.Root, "src", "audio-engine", "src", "types.ts"));

        foreach (var method in RecordingMethods.All)
        {
            typesTs.ShouldContain($"  {method}(", customMessage: $"'{method}' is invoked but missing in types.ts");
        }
    }

    private static CapturedTake Take() => new()
    {
        CaptureKey = "c1",
        SessionId = Guid.NewGuid(),
        Partial = false,
        StartSample = 0,
        LengthSamples = 441_000,
        Channels = 1,
        InputMode = InputChannelModeDto.Mono1,
        LatencySamples = 1102,
        LatencyMethod = LatencyMethodDto.Loopback,
        PeakDbfs = -6,
        ClippedSamples = 0,
        Dropouts = 0,
        RecordedAt = DateTimeOffset.UnixEpoch,
    };

    private static string Json(object? value) => JsonSerializer.Serialize(value, Web);
}
