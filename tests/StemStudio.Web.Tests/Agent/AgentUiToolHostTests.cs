using Microsoft.Extensions.DependencyInjection;
using StemStudio.Contracts.Agent;
using StemStudio.Web.Agent;
using StemStudio.Web.Help;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Agent;

/// <summary>The UI tool round trip of DELTA.md section 4: dispatch to the open mixer or the layout tools, readable failures.</summary>
public sealed class AgentUiToolHostTests : PageTestContext
{
    private AgentUiToolHost Host => Services.GetRequiredService<AgentUiToolHost>();

    private static UiRequestedEventDto Request(string tool, string arguments = "{}") => new()
    {
        StepId = AgentData.StepId,
        Tool = tool,
        Arguments = AgentData.Json(arguments),
        TimeoutSeconds = AgentContractLimits.UiToolTimeoutSeconds,
    };

    [Fact]
    public async Task MixerTool_GoesToTheOpenMixer()
    {
        var controls = new FakeMixerControls();
        using var registration = Host.RegisterMixer(new MixerUiTools(controls));

        var result = await Host.ExecuteAsync(Request("mixer_set_channel", """{"stem":"vocals","mute":true}"""));

        (result.Ok, result.Error).ShouldBe((true, null));
        result.Result!.Value.GetProperty("current").GetProperty("mute").GetBoolean().ShouldBeTrue();
        controls.State.Channel("vocals").Mute.ShouldBeTrue();
        Host.OpenMixId(TestData.TrackId).ShouldBeNull();
    }

    [Theory]
    [InlineData("mixer_set_master", """{"gainDb":-3}""")]
    [InlineData("show_karaoke", """{"enabled":true}""")]
    public async Task MixerTool_WithoutAnOpenMixer_SaysTheMixerIsNotOpen(string tool, string arguments)
    {
        var result = await Host.ExecuteAsync(Request(tool, arguments));

        (result.Ok, result.Error, result.Result).ShouldBe((false, "The mixer of the track is not open", null));
    }

    [Fact]
    public async Task MixerTool_ForAnotherTrack_SaysThatMixerIsNotOpen()
    {
        using var registration = Host.RegisterMixer(new MixerUiTools(new FakeMixerControls()));

        var result = await Host.ExecuteAsync(Request("mixer_get_state", $$"""{"trackId":"{{AgentData.TrackId}}"}"""));

        result.Error.ShouldBe("The mixer of that track is not open (the mixer of Never Gonna Give You Up is)");
    }

    [Fact]
    public async Task ClosingTheMixer_Unregisters_ButAnOlderRegistrationDoesNotRemoveANewerOne()
    {
        var first = Host.RegisterMixer(new MixerUiTools(new FakeMixerControls()));
        using var second = Host.RegisterMixer(new MixerUiTools(new FakeMixerControls()));

        first.Dispose();

        (await Host.ExecuteAsync(Request("mixer_get_state"))).Ok.ShouldBeTrue();
        second.Dispose();
        (await Host.ExecuteAsync(Request("mixer_get_state"))).Ok.ShouldBeFalse();
    }

    [Fact]
    public async Task UnknownTool_HasNoHandler()
    {
        var result = await Host.ExecuteAsync(Request("format_disk"));

        result.Error.ShouldBe("No open page can run format_disk");
    }

    [Fact]
    public async Task HandlerFailure_IsReturnedAsAReadableError()
    {
        using var registration = Host.RegisterMixer(new MixerUiTools(new FakeMixerControls()));

        var result = await Host.ExecuteAsync(Request("mixer_set_channel", """{"stem":42}"""));

        (result.Ok, result.Error).ShouldBe((false, "The argument 'stem' is required."));
    }

    [Fact]
    public async Task OpenTrack_Navigates()
    {
        var result = await Host.ExecuteAsync(Request("open_track", $$"""{"trackId":"{{AgentData.TrackId}}"}"""));

        result.Ok.ShouldBeTrue();
        result.Result!.Value.GetProperty("opened").GetBoolean().ShouldBeTrue();
        NavigatedTo.ShouldEndWith($"/tracks/{AgentData.TrackId}");
    }

    [Theory]
    [InlineData(false, "/mixer", false)]
    [InlineData(true, "/mixer?mixId=f1b2c3d4-0000-4000-8000-000000000001", true)]
    public async Task OpenMixer_NavigatesWithTheOptionalMix(bool withMix, string suffix, bool loaded)
    {
        var mix = withMix ? $$""","mixId":"{{AgentData.MixId}}" """ : string.Empty;

        var result = await Host.ExecuteAsync(Request("open_mixer", $$"""{"trackId":"{{AgentData.TrackId}}"{{mix}}}"""));

        NavigatedTo.ShouldEndWith($"/tracks/{AgentData.TrackId}{suffix}");
        result.Result!.Value.TryGetProperty("mixLoaded", out var mixLoaded).ShouldBe(loaded);
        if (loaded)
        {
            mixLoaded.GetBoolean().ShouldBeTrue();
        }
    }

    [Theory]
    [InlineData("import", "/")]
    [InlineData("tracks", "/tracks")]
    [InlineData("mixes", "/mixes")]
    [InlineData("library", "/library")]
    [InlineData("styles", "/styles")]
    [InlineData("generations", "/generations")]
    [InlineData("lyrics", "/lyrics")]
    [InlineData("songs", "/songs/new")]
    [InlineData("voices", "/voices")]
    [InlineData("settings", "/settings")]
    public async Task Navigate_OpensTheNamedPage(string page, string path)
    {
        var result = await Host.ExecuteAsync(Request("navigate", $$"""{"page":"{{page}}"}"""));

        result.Result!.Value.GetProperty("opened").GetBoolean().ShouldBeTrue();
        new Uri(NavigatedTo).AbsolutePath.ShouldBe(path);
    }

    [Fact]
    public async Task Navigate_UnknownPage_IsNotOpened()
    {
        var result = await Host.ExecuteAsync(Request("navigate", """{"page":"admin"}"""));

        result.Ok.ShouldBeTrue();
        result.Result!.Value.GetProperty("opened").GetBoolean().ShouldBeFalse();
        result.Result!.Value.GetProperty("reason").GetString().ShouldBe("There is no page 'admin'.");
    }

    [Fact]
    public async Task OpenTrack_WithoutATrackId_Fails() =>
        (await Host.ExecuteAsync(Request("open_track"))).Error.ShouldBe("The argument 'trackId' is required.");

    [Fact]
    public async Task OpenHelp_ShowsTheTopic_OrSaysItIsUnknown()
    {
        var opened = await Host.ExecuteAsync(Request("open_help", """{"topicId":"mixer-pan"}"""));
        var unknown = await Host.ExecuteAsync(Request("open_help", """{"topicId":"no-such-topic"}"""));

        opened.Result!.Value.GetProperty("opened").GetBoolean().ShouldBeTrue();
        var help = Services.GetRequiredService<HelpContext>();
        (help.IsOpen, help.TopicId).ShouldBe((true, "mixer-pan"));
        unknown.Result!.Value.GetProperty("opened").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task SearchHelp_ReturnsAtMostFiveTopics()
    {
        var result = await Host.ExecuteAsync(Request("search_help", """{"query":"pan"}"""));

        var hits = result.Result!.Value.EnumerateArray().ToList();
        hits.Count.ShouldBeInRange(1, 5);
        hits[0].GetProperty("topicId").GetString().ShouldBe("mixer-pan");
        hits[0].GetProperty("title").GetString().ShouldNotBeNullOrEmpty();
        hits[0].GetProperty("summary").GetString()!.Length.ShouldBeLessThanOrEqualTo(300);
    }
}
