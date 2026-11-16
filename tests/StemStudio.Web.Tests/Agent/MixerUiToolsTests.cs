using System.Text.Json;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Agent;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Agent;

/// <summary>Mixer UI tools (tool catalogue 1.0.0, feature 001): they change MixerState through the page's own control
/// handlers and return previous and current values (FR-005).</summary>
public sealed class MixerUiToolsTests
{
    private readonly FakeMixerControls _controls = new();
    private readonly MixerUiTools _tools;

    public MixerUiToolsTests() => _tools = new MixerUiTools(_controls);

    private static UiToolArguments Args(string json = "{}") => new(AgentData.Json(json));

    private static JsonElement Json(UiToolResult result) => JsonSerializer.SerializeToElement(result.Result, AgentApiClient.Json);

    private Task<UiToolResult> RunAsync(string tool, string json = "{}") => _tools.ExecuteAsync(tool, Args(json));

    [Fact]
    public void Handles_TheMixerTools_AndKnowsItsTrack()
    {
        MixerUiTools.Names.ShouldBe(["mixer_get_state", "mixer_set_channel", "mixer_set_master", "mixer_reset", "transport", "set_loop", "set_metronome"], ignoreOrder: true);
        (_tools.Handles("set_loop"), _tools.Handles("open_track"), _tools.TrackId, _tools.Title).ShouldBe((true, false, TestData.TrackId, "Never Gonna Give You Up"));
    }

    [Fact]
    public async Task SetChannel_AbsoluteGain_UsesTheFaderHandler_AndReturnsPreviousAndCurrent()
    {
        var result = await RunAsync("mixer_set_channel", """{"stem":"vocals","gainDb":-6}""");

        result.Ok.ShouldBeTrue();
        _controls.Calls.ShouldBe(["volume vocals", "refresh"]);
        _controls.State.Channel("vocals").Volume.ShouldBe(0.501, 0.001);
        var json = Json(result);
        json.GetProperty("stem").GetString().ShouldBe("vocals");
        json.GetProperty("previous").GetProperty("gainDb").GetDouble().ShouldBe(-1.9);
        json.GetProperty("current").GetProperty("gainDb").GetDouble().ShouldBe(-6);
    }

    [Fact]
    public async Task SetChannel_RelativeGain_IsAddedToTheCurrentGain()
    {
        var result = await RunAsync("mixer_set_channel", """{"stem":"guitar","gainChangeDb":-3}""");

        Json(result).GetProperty("current").GetProperty("gainDb").GetDouble().ShouldBe(-4.9);
    }

    [Fact]
    public async Task SetChannel_GainBelowMinus60_SilencesTheFader()
    {
        await RunAsync("mixer_set_channel", """{"stem":"bass","gainDb":-60}""");

        _controls.State.Channel("bass").Volume.ShouldBe(0);
        Json(await RunAsync("mixer_get_state")).GetProperty("channels").EnumerateArray()
            .Single(c => c.GetProperty("stem").GetString() == "bass").GetProperty("gainDb").GetDouble().ShouldBe(-60);
    }

    [Fact]
    public async Task SetChannel_PanMuteSolo_ToggleOnlyWhatChanges()
    {
        _controls.State.ToggleMute("drums");

        var result = await RunAsync("mixer_set_channel", """{"stem":"drums","pan":-0.5,"mute":true,"solo":true}""");

        _controls.Calls.ShouldBe(["pan drums", "solo drums", "refresh"]);
        var current = Json(result).GetProperty("current");
        (current.GetProperty("pan").GetDouble(), current.GetProperty("mute").GetBoolean(), current.GetProperty("solo").GetBoolean()).ShouldBe((-0.5, true, true));
    }

    [Theory]
    [InlineData("""{"stem":"vocals","gainDb":-6,"gainChangeDb":-3}""", "Use gainDb or gainChangeDb, not both.")]
    [InlineData("""{"stem":"kazoo","gainDb":-6}""", "This track has no channel 'kazoo'.")]
    [InlineData("""{"gainDb":-6}""", "The argument 'stem' is required.")]
    public async Task SetChannel_InvalidArguments_Fail(string json, string error)
    {
        var result = await RunAsync("mixer_set_channel", json);

        (result.Ok, result.Error).ShouldBe((false, error));
        _controls.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task SetMaster_AbsoluteAndRelative()
    {
        var absolute = await RunAsync("mixer_set_master", """{"gainDb":-6}""");
        var relative = await RunAsync("mixer_set_master", """{"gainChangeDb":3}""");

        (Json(absolute).GetProperty("previous").GetDouble(), Json(absolute).GetProperty("current").GetDouble()).ShouldBe((0, -6));
        Json(relative).GetProperty("current").GetDouble().ShouldBe(-3);
        _controls.Calls.Count(c => c == "master").ShouldBe(2);
    }

    [Fact]
    public async Task Reset_SetsEveryChannelTo0DbCentreUnmutedUnsoloed()
    {
        await RunAsync("mixer_set_channel", """{"stem":"vocals","gainDb":-12,"pan":0.5,"mute":true,"solo":true}""");

        var result = await RunAsync("mixer_reset");

        var vocals = _controls.State.Channel("vocals");
        (vocals.Volume, vocals.Pan, vocals.Mute, vocals.Solo).ShouldBe((1.0, 0.0, false, false));
        _controls.State.Channels.ShouldAllBe(c => c.Volume == 1.0);
        Json(result).GetProperty("previous").GetArrayLength().ShouldBe(6);
    }

    [Theory]
    [InlineData("""{"action":"play"}""", "play", true, 12.5)]
    [InlineData("""{"action":"pause"}""", "pause", false, 12.5)]
    [InlineData("""{"action":"stop"}""", "stop", false, 0)]
    [InlineData("""{"action":"seek","positionSeconds":42}""", "seek 42", false, 42)]
    public async Task Transport_UsesTheTransportHandlers(string json, string call, bool playing, double position)
    {
        var result = await RunAsync("transport", json);

        _controls.Calls[0].ShouldBe(call);
        (Json(result).GetProperty("playing").GetBoolean(), Json(result).GetProperty("position").GetDouble()).ShouldBe((playing, position));
    }

    [Theory]
    [InlineData("""{"action":"rewind"}""", "Unknown transport action 'rewind'.")]
    [InlineData("""{"action":"seek"}""", "The argument 'positionSeconds' is required.")]
    public async Task Transport_InvalidArguments_Fail(string json, string error) =>
        (await RunAsync("transport", json)).Error.ShouldBe(error);

    [Fact]
    public async Task SetLoop_BySeconds_SetsTheMarkersAndEnables()
    {
        var result = await RunAsync("set_loop", """{"startSeconds":10,"endSeconds":20,"enabled":true}""");

        result.Ok.ShouldBeTrue();
        _controls.Calls.ShouldBe(["loop-start", "loop-end", "loop-toggle", "refresh"]);
        (_controls.State.LoopStart, _controls.State.LoopEnd, _controls.State.LoopEnabled).ShouldBe((10.0, 20.0, true));
        Json(result).GetProperty("previous").ValueKind.ShouldBe(JsonValueKind.Null);
        Json(result).GetProperty("current").GetProperty("enabled").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task SetLoop_ByBars_UsesTheBeatGridDownbeats()
    {
        await RunAsync("set_loop", """{"startBar":1,"endBar":1,"enabled":true}""");

        (_controls.State.LoopStart, _controls.State.LoopEnd).ShouldBe((0.52, 2.64));
    }

    [Fact]
    public async Task SetLoop_LastBar_EndsAtTheTrackEnd_AndDisablingKeepsTheMarkers()
    {
        await RunAsync("set_loop", """{"startBar":2,"endBar":2,"enabled":true}""");
        await RunAsync("set_loop", """{"enabled":false}""");

        (_controls.State.LoopStart, _controls.State.LoopEnd, _controls.State.LoopEnabled).ShouldBe((2.64, 213.04, false));
    }

    [Theory]
    [InlineData("""{"startBar":9,"endBar":9,"enabled":true}""", "Bar 9 is outside the beat grid of this track (2 bars).")]
    [InlineData("""{"startSeconds":30,"endSeconds":20,"enabled":true}""", null)]
    public async Task SetLoop_Invalid_Fails(string json, string? error)
    {
        var result = await RunAsync("set_loop", json);

        result.Ok.ShouldBeFalse();
        result.Error.ShouldBe(error ?? _controls.State.LoopError);
    }

    [Fact]
    public async Task SetLoop_ByBars_WithoutABeatGrid_Fails()
    {
        var tools = new MixerUiTools(new FakeMixerControls(TestData.Track() with { BeatGrid = null }));

        var result = await tools.ExecuteAsync("set_loop", Args("""{"startBar":1,"endBar":2,"enabled":true}"""));

        result.Error.ShouldBe("This track has no beat grid; give the loop in seconds.");
    }

    [Fact]
    public async Task SetMetronome_ChangesOnlyTheGivenSettings()
    {
        var result = await RunAsync("set_metronome", """{"enabled":true,"volume":0.4,"beatsPerBar":3,"offsetMs":-20}""");

        var metronome = _controls.State.Metronome;
        (metronome.Enabled, metronome.Volume, metronome.BeatsPerBar, metronome.OffsetMs).ShouldBe((true, 0.4, 3, -20));
        Json(result).GetProperty("current").GetProperty("beatsPerBar").GetInt32().ShouldBe(3);
        Json(result).GetProperty("previous").GetProperty("beatsPerBar").GetInt32().ShouldBe(4);
        _controls.Calls.ShouldBe(["metronome", "refresh"]);
    }

    [Fact]
    public async Task GetState_ReportsTheOpenMixer()
    {
        _controls.Playing = true;

        var json = Json(await RunAsync("mixer_get_state"));

        json.GetProperty("trackId").GetGuid().ShouldBe(TestData.TrackId);
        json.GetProperty("unsaved").GetBoolean().ShouldBeFalse();
        json.GetProperty("channels").GetArrayLength().ShouldBe(6);
        (json.GetProperty("playing").GetBoolean(), json.GetProperty("position").GetDouble(), json.GetProperty("master").GetDouble()).ShouldBe((true, 12.5, 0));
        json.GetProperty("metronome").GetProperty("beatsPerBar").GetInt32().ShouldBe(4);
        _controls.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task UnknownTool_Fails() =>
        (await RunAsync("show_karaoke")).Error.ShouldBe("The mixer cannot run show_karaoke.");

    [Fact]
    public void Arguments_ReadNumbersBooleansTextAndIds()
    {
        var args = Args($$"""{"n":1.5,"i":3,"b":true,"s":"x","g":"{{TestData.TrackId}}","bad":"y"}""");

        (args.Number("n"), args.Int("i"), args.Bool("b"), args.Text("s"), args.Id("g")).ShouldBe((1.5, 3, true, "x", TestData.TrackId));
        (args.Number("missing"), args.Int("bad"), args.Bool("s"), args.Text("n"), args.Id("bad")).ShouldBe((null, null, null, null, null));
        new UiToolArguments(AgentData.Json("[]")).Text("x").ShouldBeNull();
    }

    [Fact]
    public void Track_WithoutDuration_HasZeroDuration()
    {
        var controls = new FakeMixerControls(TestData.Track() with { DurationSeconds = null, Status = TrackStatusDto.Completed });

        controls.State.Duration.ShouldBe(0);
    }
}
