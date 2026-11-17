using Bunit;
using Microsoft.JSInterop;
using StemStudio.Contracts.Recording;
using StemStudio.Web.Recording;

namespace StemStudio.Web.Tests.Recording;

/// <summary>015 T026: <see cref="RecordingPreferences"/> defaults, validation and persistence (data-model 4).</summary>
public sealed class RecordingPreferencesTests : IAsyncDisposable
{
    private readonly BunitContext _context = new();

    public ValueTask DisposeAsync() => _context.DisposeAsync();

    [Fact]
    public void Defaults_FollowDataModel4()
    {
        var defaults = RecordingSettings.Default;

        defaults.DeviceLabel.ShouldBeNull();
        defaults.InputMode.ShouldBe(InputChannelModeDto.Mono1);
        defaults.MuteOriginal.ShouldBeTrue();
        defaults.CountInBars.ShouldBe(1);
        defaults.Monitoring.ShouldBeFalse();
        defaults.MonitoringVolume.ShouldBe(0.7);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not json")]
    [InlineData("null")]
    public void Parse_Unusable_GivesDefaults(string? stored) => RecordingPreferences.Parse(stored).ShouldBe(RecordingSettings.Default);

    [Fact]
    public void Parse_ValidRecord_KeepsEveryField()
    {
        var settings = RecordingPreferences.Parse(
            """{"deviceLabel":"USB Mic","inputMode":"stereo","part":"drums","muteOriginal":false,"countInBars":2,"monitoring":true,"monitoringVolume":0.3}""");

        settings.ShouldBe(new RecordingSettings
        {
            DeviceLabel = "USB Mic",
            InputMode = InputChannelModeDto.Stereo,
            Part = RecordingPartDto.Drums,
            MuteOriginal = false,
            CountInBars = 2,
            Monitoring = true,
            MonitoringVolume = 0.3,
        });
    }

    [Fact]
    public void Parse_OutOfRangeFields_FallBackOneByOne()
    {
        var settings = RecordingPreferences.Parse(
            $$"""{"deviceLabel":"{{new string('x', 201)}}","countInBars":3,"monitoringVolume":1.5,"part":"guitar"}""");

        settings.DeviceLabel.ShouldBeNull();
        settings.CountInBars.ShouldBe(1);
        settings.MonitoringVolume.ShouldBe(0.7);
        settings.Part.ShouldBe(RecordingPartDto.Guitar);
        RecordingPreferences.Parse("""{"deviceLabel":"  ","countInBars":-1,"monitoringVolume":-0.1}""").ShouldBe(RecordingSettings.Default);
    }

    [Fact]
    public void Parse_UnknownEnumValue_GivesDefaults() =>
        RecordingPreferences.Parse("""{"inputMode":"surround"}""").ShouldBe(RecordingSettings.Default);

    [Fact]
    public async Task SaveThenLoad_RoundTripsThroughLocalStorage()
    {
        string? stored = null;
        _context.JSInterop.SetupVoid("localStorage.setItem", i => { stored = (string)i.Arguments[1]!; return true; }).SetVoidResult();
        var preferences = new RecordingPreferences(_context.JSInterop.JSRuntime);
        var settings = RecordingSettings.Default with { DeviceLabel = "Interface 1-2", Part = RecordingPartDto.Bass };

        await preferences.SaveAsync(settings);
        _context.JSInterop.Setup<string?>("localStorage.getItem", RecordingPreferences.StorageKey).SetResult(stored);

        (await preferences.LoadAsync()).ShouldBe(settings);
        stored.ShouldNotBeNull().ShouldContain("\"part\":\"bass\"");
        _context.JSInterop.VerifyInvoke("localStorage.setItem").Arguments[0].ShouldBe("stemstudio.recording");
    }

    [Fact]
    public async Task Load_StorageUnavailable_GivesDefaults()
    {
        _context.JSInterop.Setup<string?>("localStorage.getItem", _ => true).SetException(new JSException("blocked"));

        (await new RecordingPreferences(_context.JSInterop.JSRuntime).LoadAsync()).ShouldBe(RecordingSettings.Default);
    }
}
