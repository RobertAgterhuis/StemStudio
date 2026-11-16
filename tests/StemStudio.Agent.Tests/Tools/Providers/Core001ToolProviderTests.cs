using System.Text.Json;
using System.Text.Json.Nodes;
using NSubstitute;
using StemStudio.Agent.Ports;
using StemStudio.Agent.Tests.Fixtures;
using StemStudio.Agent.Tools;
using StemStudio.Agent.Tools.Providers;
using StemStudio.Contracts.Agent;
using StemStudio.Contracts.Lyrics;
using StemStudio.Contracts.Mixes;
using StemStudio.Contracts.Tracks;
using static StemStudio.Agent.Tests.Tools.Providers.ProviderTestKit;

namespace StemStudio.Agent.Tests.Tools.Providers;

/// <summary>T028: the 21 tools of feature 001 (tool catalogue 1.0.0) and their mapping to the ports (FR-024).</summary>
public sealed class Core001ToolProviderTests
{
    private static readonly Guid TrackId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid MixId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Core001ToolProvider _provider = new();
    private readonly IStemStudioActions _actions = Actions();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private AgentToolDescriptor T(string name) => Tool(_provider, name);

    private static UiRequester OpenMixer(MixSettingsDto settings, List<string>? calls = null) => (tool, _, _) =>
    {
        calls?.Add(tool);
        var state = new JsonObject
        {
            ["trackId"] = TrackId.ToString(),
            ["unsaved"] = true,
            ["position"] = 12.5,
            ["playing"] = false,
            ["settings"] = JsonSerializer.SerializeToNode(settings, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
        };
        return Task.FromResult(new UiToolResult { Ok = true, Result = state.ToJsonString() });
    };

    [Fact]
    public void Tools_MatchTheCatalogue() => MatchesCatalogue(_provider, "001");

    [Fact]
    public void Arguments_FollowTheCatalogue()
    {
        Valid(T("mixer_set_channel"), """{"stem":"vocals","gainDb":-60}""").ShouldBeTrue();
        Valid(T("mixer_set_channel"), """{"stem":"drums.kick","gainChangeDb":3}""").ShouldBeTrue();
        Valid(T("mixer_set_channel"), """{"stem":"vocals","gainDb":3,"gainChangeDb":3}""").ShouldBeFalse();
        Valid(T("mixer_set_channel"), """{"stem":"vocals","gainDb":13}""").ShouldBeFalse();
        Valid(T("mixer_set_channel"), """{"stem":"cowbell"}""").ShouldBeFalse();
        Valid(T("mixer_set_master"), """{"gainChangeDb":-3}""").ShouldBeTrue();
        Valid(T("mixer_set_master"), """{"gainDb":0,"gainChangeDb":1}""").ShouldBeFalse();
        Valid(T("transport"), """{"action":"seek","positionSeconds":30}""").ShouldBeTrue();
        Valid(T("transport"), """{"action":"rewind"}""").ShouldBeFalse();
        Valid(T("set_loop"), """{"startBar":1,"endBar":5,"enabled":true}""").ShouldBeTrue();
        Valid(T("set_metronome"), """{"beatsPerBar":13}""").ShouldBeFalse();
        Valid(T("save_mix_as"), $$"""{"trackId":"{{TrackId}}","name":"","fromOpenMixer":true}""").ShouldBeFalse();
        Valid(T("import_from_url"), """{"url":"https://youtu.be/x"}""").ShouldBeTrue();
        Valid(T("search_library"), """{"status":"processing"}""").ShouldBeTrue();
        Valid(T("get_track"), """{"trackId":"nope"}""").ShouldBeFalse();
    }

    [Fact]
    public void Policy_ArgumentsAreDeclared()
    {
        T("import_from_url").UrlArguments.ShouldBe(["url"]);
        T("delete_track").TargetArgument.ShouldBe("trackId");
        T("delete_mix").TargetArgument.ShouldBe("mixId");
    }

    [Fact]
    public async Task SearchLibrary_FiltersCaseAndDiacriticsInsensitively()
    {
        _actions.Tracks.ListTracksAsync(Arg.Any<CancellationToken>()).Returns(
        [
            Dtos.Track(title: "Halo", artist: "Beyoncé"),
            Dtos.Track(title: "Crazy in Love", artist: "BEYONCE"),
            Dtos.Track(title: "Hero of the Day", artist: "Metallica"),
        ]);

        var result = await InvokeAsync(T("search_library"), """{"query":"beyonce"}""", _actions);

        var items = result.Projection.AsArray();
        items.Select(item => item!["untrusted_text"]!.GetValue<string>()).ShouldBe(["Halo - Beyoncé", "Crazy in Love - BEYONCE"]);
        Json(result.Projection).ShouldNotContain("youtube.com");
        items[0]!["bpm"]!.GetValue<double>().ShouldBe(120);
    }

    [Fact]
    public async Task SearchLibrary_FiltersByStatus_AndKeepsAtMostTen()
    {
        var tracks = Enumerable.Range(0, 12).Select(i => Dtos.Track(title: "Song " + i, status: TrackStatusDto.Separating, progress: 40)).ToList();
        tracks.Add(Dtos.Track(title: "Done"));
        _actions.Tracks.ListTracksAsync(Arg.Any<CancellationToken>()).Returns(tracks);

        var processing = (await InvokeAsync(T("search_library"), """{"status":"processing"}""", _actions)).Projection.AsArray();
        var completed = (await InvokeAsync(T("search_library"), """{"status":"completed"}""", _actions)).Projection.AsArray();

        processing.Count.ShouldBe(10);
        processing[0]!["status"]!.GetValue<string>().ShouldBe("processing");
        completed.Single()!["untrusted_text"]!.GetValue<string>().ShouldBe("Done - Metallica");
    }

    [Fact]
    public async Task GetTrack_ProjectsTheWhitelistedFields()
    {
        var track = Dtos.Track(TrackId).WithDetail(DetailPhaseStatusDto.Completed, 100) with { LyricsStatus = LyricsStatusDto.Found };
        _actions.Tracks.GetTrackAsync(TrackId, Arg.Any<CancellationToken>()).Returns(track);

        var result = await InvokeAsync(T("get_track"), $$"""{"trackId":"{{TrackId}}"}""", _actions);

        var json = Json(result.Projection);
        result.Projection["untrusted_text"]!.GetValue<string>().ShouldBe("Hero of the Day - Metallica");
        result.Projection["stems"]!.AsArray().Select(stem => stem!.GetValue<string>()).ShouldBe(["vocals", "drums"]);
        result.Projection["detailStatus"]!.GetValue<string>().ShouldBe("completed");
        result.Projection["lyricsStatus"]!.GetValue<string>().ShouldBe("found");
        result.Projection["timeSignature"]!.GetValue<string>().ShouldBe("4/4");
        json.ShouldNotContain("/api/tracks");
        json.ShouldNotContain("youtube.com");
    }

    [Fact]
    public async Task ImportFromUrl_StartsTheImport_WithAJobAndACompensation()
    {
        _actions.Tracks.ImportFromUrlAsync("https://youtu.be/x", Arg.Any<CancellationToken>()).Returns(Dtos.Track(TrackId, status: TrackStatusDto.Queued, progress: 0));

        var result = await InvokeAsync(T("import_from_url"), """{"url":"https://youtu.be/x"}""", _actions);

        result.Projection["trackId"]!.GetValue<string>().ShouldBe(TrackId.ToString());
        result.Projection["cached"]!.GetValue<bool>().ShouldBeFalse();
        result.Projection["jobRef"]!["phase"]!.GetValue<string>().ShouldBe("standard");
        (result.Job!.Phase, result.Job.TrackId).ShouldBe((JobPhaseDto.Standard, TrackId));
        (result.Undo!.Tool, result.Undo.Arguments, result.UndoFresh).ShouldBe(("delete_track", $$"""{"trackId":"{{TrackId}}"}""", false));
    }

    [Fact]
    public async Task ImportFromUrl_ReportsACachedTrack()
    {
        _actions.Tracks.ImportFromUrlAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Dtos.Track(TrackId));

        var result = await InvokeAsync(T("import_from_url"), """{"url":"https://youtu.be/x"}""", _actions);

        result.Projection["cached"]!.GetValue<bool>().ShouldBeTrue();
    }

    [Fact]
    public async Task RetryAndDeleteTrack_CallThePorts()
    {
        _actions.Tracks.RetryTrackAsync(TrackId, Arg.Any<CancellationToken>()).Returns(Dtos.Track(TrackId, status: TrackStatusDto.Queued));

        var retry = await InvokeAsync(T("retry_track"), $$"""{"trackId":"{{TrackId}}"}""", _actions);
        var delete = await InvokeAsync(T("delete_track"), $$"""{"trackId":"{{TrackId}}"}""", _actions);

        retry.Job!.Phase.ShouldBe(JobPhaseDto.Standard);
        retry.Projection["status"]!.GetValue<string>().ShouldBe("queued");
        Json(delete.Projection).ShouldBe("""{"deleted":true}""");
        await _actions.Tracks.Received(1).DeleteTrackAsync(TrackId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListMixes_PassesTheTrackAndMarksNamesUntrusted()
    {
        _actions.Mixes.ListMixesAsync(TrackId, Arg.Any<CancellationToken>()).Returns([new MixSummaryDto(MixId, TrackId, "Hero", "Practice", Dtos.At)]);

        var result = await InvokeAsync(T("list_mixes"), $$"""{"trackId":"{{TrackId}}"}""", _actions);

        var item = result.Projection.AsArray().Single()!;
        (item["mixId"]!.GetValue<string>(), item["untrusted_text"]!.GetValue<string>()).ShouldBe((MixId.ToString(), "Practice"));
    }

    [Fact]
    public async Task GetMix_ConvertsVolumesToDecibels()
    {
        _actions.Mixes.GetMixAsync(MixId, Arg.Any<CancellationToken>()).Returns(Dtos.Mix(MixId, TrackId) with
        {
            Settings = Dtos.Settings() with { Channels = [new ChannelSettingsDto("vocals", 1, 0, false, false), new ChannelSettingsDto("drums", 0, 0, true, false)] },
        });

        var result = await InvokeAsync(T("get_mix"), $$"""{"mixId":"{{MixId}}"}""", _actions);

        var channels = result.Projection["channels"]!.AsArray();
        channels[0]!["gainDb"]!.GetValue<double>().ShouldBe(0);
        channels[1]!["gainDb"]!.GetValue<double>().ShouldBe(-60);
        channels[1]!["mute"]!.GetValue<bool>().ShouldBeTrue();
        result.Projection["master"]!["gainDb"]!.GetValue<double>().ShouldBe(-6.0, 0.1);
        result.Projection["loop"]!["enabled"]!.GetValue<bool>().ShouldBeTrue();
        result.Projection["untrusted_text"]!.GetValue<string>().ShouldBe("Practice");
    }

    [Fact]
    public async Task SaveMixAs_ReadsTheOpenMixer_AndRecordsAFreshCompensation()
    {
        var calls = new List<string>();
        CreateMixRequest? sent = null;
        _actions.Mixes.CreateMixAsync(Arg.Do<CreateMixRequest>(request => sent = request), Arg.Any<CancellationToken>()).Returns(Dtos.Mix(MixId, TrackId, "Loud drums"));

        var result = await InvokeAsync(T("save_mix_as"), $$"""{"trackId":"{{TrackId}}","name":"Loud drums","fromOpenMixer":true}""", _actions, OpenMixer(Dtos.Settings("old"), calls));

        calls.ShouldBe(["mixer_get_state"]);
        (sent!.TrackId, sent.Settings.Name, sent.Settings.MasterVolume).ShouldBe((TrackId, "Loud drums", 0.5));
        Json(result.Projection).ShouldBe($$"""{"mixId":"{{MixId}}","name":"Loud drums"}""");
        (result.Undo!.Tool, result.Undo.Arguments, result.UndoFresh).ShouldBe(("delete_mix", $$"""{"mixId":"{{MixId}}"}""", true));
    }

    [Fact]
    public async Task SaveMixAs_WithoutAnOpenMixer_IsAReadableError()
    {
        UiRequester closed = (_, _, _) => Task.FromResult(new UiToolResult { Ok = false, Error = "No mixer open" });

        var noUi = await Should.ThrowAsync<AgentActionException>(() => InvokeAsync(T("save_mix_as"), $$"""{"trackId":"{{TrackId}}","name":"x","fromOpenMixer":true}""", _actions));
        var notOpen = await Should.ThrowAsync<AgentActionException>(() => InvokeAsync(T("save_mix_as"), $$"""{"trackId":"{{TrackId}}","name":"x","fromOpenMixer":true}""", _actions, closed));

        noUi.Message.ShouldBe("No mixer is open; use open_mixer first.");
        notOpen.Message.ShouldBe("No mixer is open; use open_mixer first.");
    }

    [Fact]
    public async Task UpdateMix_KeepsTheName_AndRecordsThePreviousVersion()
    {
        MixSettingsDto? sent = null;
        _actions.Mixes.GetMixAsync(MixId, Arg.Any<CancellationToken>()).Returns(Dtos.Mix(MixId, TrackId, "Practice"));
        _actions.Mixes.UpdateMixAsync(MixId, Arg.Do<MixSettingsDto>(settings => sent = settings), Arg.Any<CancellationToken>())
            .Returns(Dtos.Mix(MixId, TrackId) with { UpdatedAt = Dtos.At.AddMinutes(1) });

        var result = await InvokeAsync(T("update_mix"), $$"""{"mixId":"{{MixId}}","fromOpenMixer":true}""", _actions, OpenMixer(Dtos.Settings("from mixer") with { MasterVolume = 0.9 }));

        (sent!.Name, sent.MasterVolume).ShouldBe(("Practice", 0.9));
        result.Projection["updatedAt"].ShouldNotBeNull();
        result.Undo!.Tool.ShouldBe("update_mix");
        var undo = JsonDocument.Parse(result.Undo.Arguments).RootElement;
        undo.GetProperty("mixId").GetString().ShouldBe(MixId.ToString());
        undo.GetProperty("settings").GetProperty("masterVolume").GetDouble().ShouldBe(0.5);
    }

    [Fact]
    public async Task DuplicateRenameAndDeleteMix_CallThePorts()
    {
        var copyId = Guid.NewGuid();
        _actions.Mixes.DuplicateMixAsync(MixId, Arg.Any<CancellationToken>()).Returns(Dtos.Mix(copyId, TrackId, "Practice (copy)"));
        _actions.Mixes.GetMixAsync(MixId, Arg.Any<CancellationToken>()).Returns(Dtos.Mix(MixId, TrackId, "Practice"));
        _actions.Mixes.UpdateMixAsync(MixId, Arg.Any<MixSettingsDto>(), Arg.Any<CancellationToken>()).Returns(Dtos.Mix(MixId, TrackId, "Gig"));

        var duplicate = await InvokeAsync(T("duplicate_mix"), $$"""{"mixId":"{{MixId}}"}""", _actions);
        var rename = await InvokeAsync(T("rename_mix"), $$"""{"mixId":"{{MixId}}","name":"Gig"}""", _actions);
        var delete = await InvokeAsync(T("delete_mix"), $$"""{"mixId":"{{MixId}}"}""", _actions);

        (duplicate.Undo!.Tool, duplicate.UndoFresh).ShouldBe(("delete_mix", true));
        duplicate.Projection["name"]!.GetValue<string>().ShouldBe("Practice (copy)");
        await _actions.Mixes.Received(1).UpdateMixAsync(MixId, Arg.Is<MixSettingsDto>(settings => settings.Name == "Gig"), Arg.Any<CancellationToken>());
        (rename.Undo!.Tool, rename.Undo.Arguments).ShouldBe(("rename_mix", $$"""{"mixId":"{{MixId}}","name":"Practice"}"""));
        Json(delete.Projection).ShouldBe("""{"deleted":true}""");
        await _actions.Mixes.Received(1).DeleteMixAsync(MixId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void MixerGetState_IsProjectedInDecibels()
    {
        var page = JsonSerializer.SerializeToNode(new { trackId = TrackId, mixId = MixId, unsaved = false, position = 3.0, playing = true, karaoke = false, settings = Dtos.Settings() },
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!.ToJsonString();

        var result = T("mixer_get_state").MapUiResult!(Element("{}"), Element(page));

        result.Projection["trackId"]!.GetValue<string>().ShouldBe(TrackId.ToString());
        result.Projection["playing"]!.GetValue<bool>().ShouldBeTrue();
        result.Projection["channels"]!.AsArray()[1]!["gainDb"]!.GetValue<double>().ShouldBe(-6.0, 0.1);
        result.Projection["master"]!["gainDb"]!.GetValue<double>().ShouldBe(-6.0, 0.1);
        result.Projection["metronome"]!["beatsPerBar"]!.GetValue<int>().ShouldBe(4);
        result.Projection["karaoke"]!.GetValue<bool>().ShouldBeFalse();
    }

    [Fact]
    public void MixerGetState_WithoutSettings_PassesThePageResult()
    {
        var result = T("mixer_get_state").MapUiResult!(Element("{}"), Element("""{"trackId":"x","unsaved":true}"""));

        result.Projection["unsaved"]!.GetValue<bool>().ShouldBeTrue();
    }
}
