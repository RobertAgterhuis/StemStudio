using System.Text.Json;
using Bunit;
using Microsoft.JSInterop;
using StemStudio.Contracts.Mixes;
using StemStudio.Web.Audio;

namespace StemStudio.Web.Tests.Audio;

/// <summary>Engine contract 2.3.0 practice calls (feature 012, T029): setPractice, getPracticeStatus, OnPracticeChanged.</summary>
public sealed partial class AudioEngineInteropTests
{
    /// <summary>The <c>PracticeStatus</c> JSON exactly as the engine sends it (types.ts 2.3.0).</summary>
    private const string EngineStatusJson = """
        {
          "speedPercent": 80, "transposeSemitones": -2, "stretch": "active", "stretchReason": null,
          "latencyMs": 106.4, "underruns": 3,
          "trainer": { "state": "running", "pass": 3, "speedPercent": 80 },
          "countIn": { "available": true, "active": true, "beatsRemaining": 2, "beatsTotal": 4 }
        }
        """;

    private static readonly PracticeSettingsDto Slowed = new(75, -2, new SpeedTrainerSettingsDto(true, 60, 5, 100, 2), new CountInSettingsDto(1, true));

    [Fact]
    public async Task SetPractice_SendsTheSettings_InTheContractShape()
    {
        await _interop.InitializeAsync();

        await _interop.SetPracticeAsync(Slowed);

        JsonSerializer.Serialize(_engine.VerifyInvoke("setPractice").Arguments.Single(), JsonSerializerOptions.Web).ShouldBe(
            """{"speedPercent":75,"transposeSemitones":-2,"trainer":{"enabled":true,"startPercent":60,"stepPercent":5,"targetPercent":100,"passesPerStep":2},"countIn":{"bars":1,"everyPass":true}}""");
    }

    [Fact]
    public async Task SetPractice_InvalidSettings_IsSurfacedAsAudioEngineException()
    {
        _engine.SetupVoid("setPractice", _ => true).SetException(new JSException("Invalid practice settings: speedPercent out of range"));
        await _interop.InitializeAsync();

        var exception = await Should.ThrowAsync<AudioEngineException>(() => _interop.SetPracticeAsync(Slowed));

        exception.Message.ShouldBe("Invalid practice settings: speedPercent out of range");
    }

    [Fact]
    public async Task SetPractice_TimeStretchUnavailable_RaisesAnUnavailableStatus_InsteadOfThrowing()
    {
        var statuses = new List<PracticeStatus>();
        _interop.PracticeChanged += (_, status) => statuses.Add(status);
        _engine.SetupVoid("setPractice", _ => true)
            .SetException(new JSException("Time stretch unavailable: AudioWorklet is not supported\nError: at setPractice (audio-engine.js:1:2)"));
        await _interop.InitializeAsync();

        await Should.NotThrowAsync(() => _interop.SetPracticeAsync(Slowed));

        var status = statuses.ShouldHaveSingleItem();
        status.Stretch.ShouldBe(StretchState.Unavailable);
        status.StretchReason.ShouldBe("AudioWorklet is not supported");
        (status.SpeedPercent, status.TransposeSemitones, status.Trainer.State).ShouldBe((100, 0, TrainerState.Off));
    }

    [Fact]
    public async Task GetPracticeStatus_ReturnsTheEngineSnapshot()
    {
        var snapshot = PracticeStatus.Initial with { Stretch = StretchState.Active, LatencyMs = 106 };
        _engine.Setup<PracticeStatus>("getPracticeStatus", _ => true).SetResult(snapshot);
        await _interop.InitializeAsync();

        var status = await _interop.GetPracticeStatusAsync();

        status.ShouldBe(snapshot);
        _engine.VerifyInvoke("getPracticeStatus").Arguments.ShouldBeEmpty();
    }

    [Fact]
    public void OnPracticeChanged_RaisesTheEvent_WithTheEngineStatus()
    {
        var statuses = new List<PracticeStatus>();
        _interop.PracticeChanged += (_, status) => statuses.Add(status);
        var status = JsonSerializer.Deserialize<PracticeStatus>(EngineStatusJson, JsonSerializerOptions.Web)!;

        _interop.OnPracticeChanged(status);

        statuses.ShouldBe([status]);
    }

    [Fact]
    public void OnPracticeChanged_WithoutSubscribers_DoesNotThrow() =>
        Should.NotThrow(() => _interop.OnPracticeChanged(PracticeStatus.Initial));

    [Fact]
    public void PracticeStatus_MirrorsTheEngineJson()
    {
        var status = JsonSerializer.Deserialize<PracticeStatus>(EngineStatusJson, JsonSerializerOptions.Web)!;

        (status.SpeedPercent, status.TransposeSemitones, status.Stretch, status.StretchReason).ShouldBe((80, -2, StretchState.Active, (string?)null));
        (status.LatencyMs, status.Underruns).ShouldBe((106.4, 3L));
        status.Trainer.ShouldBe(new TrainerStatus(TrainerState.Running, 3, 80));
        status.CountIn.ShouldBe(new CountInStatus(true, true, 2, 4));
    }

    [Theory]
    [InlineData("bypass", StretchState.Bypass)]
    [InlineData("loading", StretchState.Loading)]
    [InlineData("active", StretchState.Active)]
    [InlineData("unavailable", StretchState.Unavailable)]
    public void StretchState_UsesTheContractNames(string json, StretchState state)
    {
        JsonSerializer.Deserialize<StretchState>($"\"{json}\"", JsonSerializerOptions.Web).ShouldBe(state);
        JsonSerializer.Serialize(state, JsonSerializerOptions.Web).ShouldBe($"\"{json}\"");
    }

    [Theory]
    [InlineData("off", TrainerState.Off)]
    [InlineData("waiting", TrainerState.Waiting)]
    [InlineData("running", TrainerState.Running)]
    [InlineData("target", TrainerState.Target)]
    public void TrainerState_UsesTheContractNames(string json, TrainerState state)
    {
        JsonSerializer.Deserialize<TrainerState>($"\"{json}\"", JsonSerializerOptions.Web).ShouldBe(state);
        JsonSerializer.Serialize(state, JsonSerializerOptions.Web).ShouldBe($"\"{json}\"");
    }

    [Fact]
    public void Initial_IsTheNeutralBypassSnapshot()
    {
        var status = PracticeStatus.Initial;

        (status.SpeedPercent, status.TransposeSemitones, status.Stretch, status.StretchReason).ShouldBe((100, 0, StretchState.Bypass, (string?)null));
        status.Trainer.State.ShouldBe(TrainerState.Off);
        status.CountIn.Active.ShouldBeFalse();
    }
}
