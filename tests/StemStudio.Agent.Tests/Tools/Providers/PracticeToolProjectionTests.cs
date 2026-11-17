using System.Text.Json;
using System.Text.Json.Nodes;
using NSubstitute;
using StemStudio.Agent.Ports;
using StemStudio.Agent.Tests.Fixtures;
using StemStudio.Agent.Tools;
using StemStudio.Agent.Tools.Providers;
using StemStudio.Contracts.Agent;
using StemStudio.Contracts.Mixes;
using static StemStudio.Agent.Tests.Tools.Providers.ProviderTestKit;

namespace StemStudio.Agent.Tests.Tools.Providers;

/// <summary>
/// 012 integration (tool catalogue 1.3.0): the practice tempo is shown read-only as numbers in get_mix and
/// mixer_get_state, and update_mix / save_mix_as / the restore_mix undo carry it unchanged.
/// </summary>
public sealed class PracticeToolProjectionTests
{
    private static readonly Guid TrackId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid MixId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private static readonly PracticeSettingsDto Slow = new(75, -2, new SpeedTrainerSettingsDto(true, 60, 10, 90, 3), new CountInSettingsDto(2, true));
    private static readonly PracticeSettingsDto Fast = new(110, 3, SpeedTrainerSettingsDto.Default, new CountInSettingsDto(1, false));
    private const string ExpectedSlow =
        """{"speedPercent":75,"transposeSemitones":-2,"trainer":{"enabled":true,"startPercent":60,"stepPercent":10,"targetPercent":90,"passesPerStep":3},"countIn":{"bars":2,"everyPass":true}}""";

    private readonly Core001ToolProvider _provider = new();
    private readonly IStemStudioActions _actions = Actions();

    private AgentToolDescriptor T(string name) => Tool(_provider, name);

    private static UiRequester OpenMixer(MixSettingsDto settings) => (_, _, _) =>
    {
        var state = new JsonObject { ["trackId"] = TrackId.ToString(), ["unsaved"] = true, ["settings"] = JsonSerializer.SerializeToNode(settings, Web) };
        return Task.FromResult(new UiToolResult { Ok = true, Result = state.ToJsonString() });
    };

    [Fact]
    public async Task GetMix_ShowsThePracticeTempoAsNumbers()
    {
        _actions.Mixes.GetMixAsync(MixId, Arg.Any<CancellationToken>()).Returns(Dtos.Mix(MixId, TrackId) with { Settings = Dtos.Settings() with { Practice = Slow } });

        var result = await InvokeAsync(T("get_mix"), $$"""{"mixId":"{{MixId}}"}""", _actions);

        Json(result.Projection["practice"]!).ShouldBe(ExpectedSlow);
    }

    [Fact]
    public void MixerGetState_ShowsThePracticeTempo_DefaultsWhenThePageOmitsIt()
    {
        var withPractice = JsonSerializer.SerializeToNode(new { trackId = TrackId, settings = Dtos.Settings() with { Practice = Slow } }, Web)!.ToJsonString();
        var withoutPractice = """{"trackId":"x","settings":{"name":"","masterVolume":1,"channels":[],"metronome":{"enabled":false,"volume":0.8,"beatsPerBar":4,"offsetMs":0}}}""";

        var shown = T("mixer_get_state").MapUiResult!(Element("{}"), Element(withPractice));
        var defaults = T("mixer_get_state").MapUiResult!(Element("{}"), Element(withoutPractice));

        Json(shown.Projection["practice"]!).ShouldBe(ExpectedSlow);
        defaults.Projection["practice"]!["speedPercent"]!.GetValue<int>().ShouldBe(100);
    }

    [Fact]
    public async Task UpdateMix_SendsTheMixersPracticeUnchanged_AndTheUndoKeepsThePreviousPractice()
    {
        MixSettingsDto? sent = null;
        _actions.Mixes.GetMixAsync(MixId, Arg.Any<CancellationToken>()).Returns(Dtos.Mix(MixId, TrackId) with { Settings = Dtos.Settings() with { Practice = Slow } });
        _actions.Mixes.UpdateMixAsync(MixId, Arg.Do<MixSettingsDto>(settings => sent = settings), Arg.Any<CancellationToken>()).Returns(Dtos.Mix(MixId, TrackId));

        var result = await InvokeAsync(T("update_mix"), $$"""{"mixId":"{{MixId}}","fromOpenMixer":true}""", _actions, OpenMixer(Dtos.Settings() with { Practice = Fast }));

        sent!.Practice.ShouldBe(Fast);
        var undo = JsonDocument.Parse(UndoGuard.Split(result.Undo!.Arguments).Arguments).RootElement;
        undo.GetProperty("settings").GetProperty("practice").Deserialize<PracticeSettingsDto>(Web).ShouldBe(Slow);
    }

    [Fact]
    public async Task RestoreMix_WritesThePracticeOfTheUndoEntryUnchanged()
    {
        MixSettingsDto? restored = null;
        _actions.Mixes.UpdateMixAsync(MixId, Arg.Do<MixSettingsDto>(settings => restored = settings), Arg.Any<CancellationToken>()).Returns(Dtos.Mix(MixId, TrackId));
        var arguments = new JsonObject { ["mixId"] = MixId.ToString(), ["settings"] = JsonSerializer.SerializeToNode(Dtos.Settings() with { Practice = Slow }, Web) };
        var restore = UndoTools.All.Single(tool => tool.Name == "restore_mix");

        await RunAsync(restore, new ToolInvocation { Arguments = Element(arguments.ToJsonString()), Actions = _actions, Context = new PageContextDto { Page = "mixer" } });

        restored!.Practice.ShouldBe(Slow);
    }

    [Fact]
    public async Task SaveMixAs_SendsTheMixersPracticeUnchanged()
    {
        CreateMixRequest? sent = null;
        _actions.Mixes.CreateMixAsync(Arg.Do<CreateMixRequest>(request => sent = request), Arg.Any<CancellationToken>()).Returns(Dtos.Mix(MixId, TrackId, "Slow"));

        await InvokeAsync(T("save_mix_as"), $$"""{"trackId":"{{TrackId}}","name":"Slow","fromOpenMixer":true}""", _actions, OpenMixer(Dtos.Settings() with { Practice = Slow }));

        sent!.Settings.Practice.ShouldBe(Slow);
    }
}
