using NSubstitute;
using StemStudio.Agent.Policy;
using StemStudio.Agent.Ports;
using StemStudio.Agent.Tests.Fakes;
using StemStudio.Agent.Tests.Fixtures;
using StemStudio.Agent.Tools;
using StemStudio.Agent.Tools.Providers;
using StemStudio.Contracts;
using StemStudio.Contracts.Agent;
using StemStudio.Contracts.Generation;
using StemStudio.Contracts.Lyrics;
using StemStudio.Contracts.Tracks;
using static StemStudio.Agent.Tests.Fakes.TestTools;
using static StemStudio.Agent.Tests.Tools.Providers.ProviderTestKit;

namespace StemStudio.Agent.Tests.Tools.Providers;

/// <summary>T077b: the 5 own-lyrics-to-song and own-voice tools of feature 010 (FR-024, FR-025, FR-026).</summary>
public sealed class Song010ToolProviderTests
{
    private const string Lyrics = "We walk the line\nUnder neon light";
    private static readonly Guid LyricsId = Guid.NewGuid();
    private static readonly Guid GenerationId = Guid.NewGuid();
    private static readonly Guid ProfileId = Guid.NewGuid();
    private readonly IStemStudioActions _actions = Actions();

    private static GenerationDto Generation(GenerationKindDto kind = GenerationKindDto.Song) => new()
    {
        Id = GenerationId,
        Kind = kind,
        Status = GenerationStateDto.Queued,
        Progress = 0,
        CreatedAt = Dtos.At,
        UpdatedAt = Dtos.At,
    };

    private static VoiceProfileSummary Profile(string version) => new() { ProfileId = ProfileId, DisplayName = "My voice", ConsentStatementVersion = version, DurationSeconds = 42 };

    [Fact]
    public void Tools_MatchTheCatalogue_AndCarryTheGuardrails()
    {
        var provider = new SongToolProvider();
        MatchesCatalogue(provider, "010");
        Tool(provider, "save_lyrics_sheet").LyricsArguments.ShouldBe(["text"]);
        Tool(provider, "generate_song").LyricsArguments.ShouldBe(["lyricsText"]);
        Tool(provider, "generate_song").ImitationArguments.ShouldBe(["freeCaption", "delivery", "title"]);
        provider.GetTools().Select(tool => tool.Name).ShouldNotContain(name => name.Contains("create_voice") || name.Contains("consent"));
    }

    [Fact]
    public void GenerateSongSchema_AllowsInstrumentalWithoutLyrics_AndOneLyricsSource()
    {
        var song = Tool(new SongToolProvider(), "generate_song");
        Valid(song, """{"language":"en","leadVoice":"any","instrumental":true}""").ShouldBeTrue();
        Valid(song, $$"""{"language":"en","leadVoice":"female","lyricsId":"{{LyricsId}}","lyricsText":"x"}""").ShouldBeFalse();
        Valid(song, $$"""{"language":"en","leadVoice":"male","styleId":"{{Guid.NewGuid()}}","freeCaption":"warm"}""").ShouldBeFalse();
        Valid(song, """{"language":"xx-long","leadVoice":"any"}""").ShouldBeFalse();
    }

    [Fact]
    public async Task ListLyricsSheets_FiltersOnTheTitle_AndNeverReturnsText()
    {
        _actions.Songs.ListLyricsSheetsAsync(Arg.Any<CancellationToken>()).Returns([
            new LyricsSheetSummary { LyricsId = LyricsId, Title = "Neon nights", Language = "en", UpdatedAt = Dtos.At },
            new LyricsSheetSummary { LyricsId = Guid.NewGuid(), Title = "Other", Language = "nl", UpdatedAt = Dtos.At },
        ]);

        var result = await InvokeAsync(Tool(new SongToolProvider(), "list_lyrics_sheets"), """{"query":"neon"}""", _actions);

        Json(result.Projection).ShouldBe($$"""[{"lyricsId":"{{LyricsId}}","untrusted_text":"Neon nights","language":"en","updatedAt":"2026-11-16T08:00:00+00:00"}]""");
    }

    [Fact]
    public async Task SaveLyricsSheet_ProjectsNoText_AndRecordsAFreshDelete()
    {
        _actions.Songs.CreateLyricsSheetAsync(Arg.Any<LyricsInput>(), Arg.Any<CancellationToken>()).Returns(new LyricsSheetSummary { LyricsId = LyricsId, Title = "Neon", Language = "en", UpdatedAt = Dtos.At });

        var result = await InvokeAsync(Tool(new SongToolProvider(), "save_lyrics_sheet"), """{"title":"Neon","language":"en","text":"We walk the line"}""", _actions);

        Json(result.Projection).ShouldBe($$"""{"lyricsId":"{{LyricsId}}","title":"Neon","warnings":[]}""");
        (result.Undo!.Tool, result.UndoFresh).ShouldBe(("delete_lyrics_sheet", true));
        await _actions.Songs.Received(1).CreateLyricsSheetAsync(Arg.Is<LyricsInput>(input => input.Text == "We walk the line"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void LyricsNotTypedByTheUser_AreDenied()
    {
        var tool = Tool(new SongToolProvider(), "save_lyrics_sheet");

        var denied = ToolPolicy.Decide(new PolicyInput { Tool = tool, Arguments = Element("""{"title":"x","language":"en","text":"Lyrics from LRCLIB"}"""), Tainted = false, UserMessages = ["save my lyrics"] });
        var allowed = ToolPolicy.Decide(new PolicyInput { Tool = tool, Arguments = Element("""{"title":"x","language":"en","text":"We walk the line\nUnder neon light"}"""), Tainted = false, UserMessages = ["Save these lyrics:\r\n We walk the line \r\n\r\nUnder neon light"] });

        (denied.Decision, allowed.Decision).ShouldBe((StepDecisionDto.Denied, StepDecisionDto.Auto));
        denied.Reasons.ShouldBe([AgentDecisionReasons.LyricsNotFromUser]);
    }

    [Fact]
    public async Task GenerateSong_SendsTheRequest_WithGenerationPhaseAndDiscard()
    {
        SongRequest? sent = null;
        _actions.Songs.CreateSongAsync(Arg.Do<SongRequest>(request => sent = request), Arg.Any<CancellationToken>()).Returns(Generation());
        var tool = Tool(new SongToolProvider(), "generate_song");
        var json = $$"""{"lyricsId":"{{LyricsId}}","language":"en","leadVoice":"female","bpm":96,"timeSignature":3,"durationSeconds":60,"takes":2}""";

        var estimate = await tool.Estimator!(Invocation(tool, json, _actions), TestContext.Current.CancellationToken);
        var result = await InvokeAsync(tool, json, _actions);

        (sent!.LyricsId, sent.LeadVoice, sent.Bpm, sent.TimeSignature, sent.DurationSeconds, sent.Takes).ShouldBe((LyricsId, LeadVoiceDto.Female, 96, "3", 60.0, 2));
        estimate.EstimatedMinutes.ShouldBe(3.0);
        result.Projection["jobRef"]!["phase"]!.GetValue<string>().ShouldBe("generation");
        result.Undo!.Tool.ShouldBe("discard_generation");
    }

    [Fact]
    public async Task VoiceProfiles_NeverCarryPersonalData()
    {
        _actions.Voices.ListVoiceProfilesAsync(Arg.Any<CancellationToken>()).Returns([Profile(ContractLimits.ConsentStatementVersion)]);

        var result = await InvokeAsync(Tool(new SongToolProvider(), "list_voice_profiles"), "{}", _actions);

        Json(result.Projection).ShouldBe($$"""[{"profileId":"{{ProfileId}}","untrusted_text":"My voice","consentCurrent":true,"durationSeconds":42}]""");
    }

    [Fact]
    public async Task ConvertTakeVoice_NamesTheProfileOnTheCard_AndStartsTheConversion()
    {
        _actions.Voices.ListVoiceProfilesAsync(Arg.Any<CancellationToken>()).Returns([Profile(ContractLimits.ConsentStatementVersion)]);
        _actions.Voices.CreateVoiceConversionAsync(GenerationId, 1, Arg.Any<VoiceConversionRequest>(), Arg.Any<CancellationToken>()).Returns(Generation(GenerationKindDto.VoiceConversion));
        var tool = Tool(new SongToolProvider(), "convert_take_voice");
        var json = $$"""{"generationId":"{{GenerationId}}","index":1,"profileId":"{{ProfileId}}","pitchShift":-2}""";

        var estimate = await tool.Estimator!(Invocation(tool, json, _actions), TestContext.Current.CancellationToken);
        var result = await InvokeAsync(tool, json, _actions);

        estimate.Effect.ShouldBe("Convert take 1 to the voice \"My voice\"");
        await _actions.Voices.Received(1).CreateVoiceConversionAsync(GenerationId, 1, Arg.Is<VoiceConversionRequest>(request => request.ProfileId == ProfileId && request.PitchShift == -2), Arg.Any<CancellationToken>());
        result.Projection["jobRef"]!["phase"]!.GetValue<string>().ShouldBe("generation");
    }

    [Fact]
    public async Task DeleteLyricsSheet_IsAnUndoOnlyTool()
    {
        var delete = UndoTools.All.Single(tool => tool.Name == "delete_lyrics_sheet");

        await InvokeAsync(delete, $$"""{"lyricsId":"{{LyricsId}}"}""", _actions);

        await _actions.Songs.Received(1).DeleteLyricsSheetAsync(LyricsId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LibraryArtists_FeedTheImitationGuardrail()
    {
        var harness = RunnerHarness.Scripted([.. new SongToolProvider().GetTools()], Turn(Call("generate_song", """{"language":"en","leadVoice":"any","freeCaption":"a ballad like Evil Uploader"}""")), Say("ok"));
        harness.Actions.Tracks.ListTracksAsync(Arg.Any<CancellationToken>()).Returns([Dtos.Track(Guid.NewGuid()) with { Artist = "Evil Uploader" }]);

        await harness.ExecuteAsync();

        var step = harness.Runs.Steps.Single();
        (step.Decision, step.Status).ShouldBe((StepDecisionDto.Denied, Contracts.Agent.AgentStepStatusDto.Failed));
        await harness.Actions.Songs.DidNotReceive().CreateSongAsync(Arg.Any<SongRequest>(), Arg.Any<CancellationToken>());
    }
}
