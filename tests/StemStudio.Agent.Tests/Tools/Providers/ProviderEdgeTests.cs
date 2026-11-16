using System.Net;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using StemStudio.Agent.Backends;
using StemStudio.Agent.Model;
using StemStudio.Agent.Ports;
using StemStudio.Agent.Tests.Fakes;
using StemStudio.Agent.Tests.Fixtures;
using StemStudio.Agent.Tools.Providers;
using StemStudio.Contracts.Agent;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Generation;
using StemStudio.Contracts.Lyrics;
using StemStudio.Contracts.Scores;
using StemStudio.Contracts.Tracks;
using static StemStudio.Agent.Tests.Tools.Providers.ProviderTestKit;

namespace StemStudio.Agent.Tests.Tools.Providers;

/// <summary>Edge cases of the 004-010 providers and the Claude test (defaults, missing values, refused values).</summary>
public sealed class ProviderEdgeTests
{
    private static readonly Guid TrackId = Guid.NewGuid();
    private static readonly Guid Id = Guid.NewGuid();
    private readonly IStemStudioActions _actions = Actions();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public void SearchHelp_IgnoresNonArraysAndNonObjects()
    {
        var map = Tool(new HelpToolProvider(), "search_help").MapUiResult!;

        map(Element("{}"), Element("""{"x":1}""")).Projection.AsArray().ShouldBeEmpty();
        map(Element("{}"), null).Projection.AsArray().ShouldBeEmpty();
        Json(map(Element("{}"), Element("""[1,{"topicId":"a","title":2}]""")).Projection).ShouldBe("""[{"topicId":"a","title":null,"summary":null}]""");
    }

    [Theory]
    [InlineData(LyricsStatusDto.NotFound, "No lyrics were found for this track.")]
    [InlineData(LyricsStatusDto.Disabled, "Online lyrics search is switched off.")]
    [InlineData(LyricsStatusDto.Instrumental, "The track is instrumental.")]
    public async Task LyricsReasons_PerStatus(LyricsStatusDto status, string reason)
    {
        _actions.Lyrics.GetLyricsSummaryAsync(TrackId, Arg.Any<CancellationToken>()).Returns(new LyricsSummary { Status = status, Instrumental = false, Reason = "x", Origin = LyricsOriginDto.Source, Source = LyricsSourceDto.Lrclib });

        var result = await InvokeAsync(Tool(new LyricsToolProvider(), "get_lyrics_status"), $$"""{"trackId":"{{TrackId}}"}""", _actions);

        result.Projection["reason"]!.GetValue<string>().ShouldBe(reason);
        result.Projection["origin"]!.GetValue<string>().ShouldBe("source");
    }

    [Theory]
    [InlineData(MetadataStatusDto.Disabled, "Online band info lookup is switched off.")]
    [InlineData(MetadataStatusDto.Searching, "The band info lookup is still running.")]
    public async Task ArtistReasons_PerStatus(MetadataStatusDto status, string reason)
    {
        _actions.Artists.GetBandInfoAsync(TrackId, Arg.Any<CancellationToken>()).Returns(new Contracts.Artists.BandInfoDto { Status = status, Reason = "x" });

        var result = await InvokeAsync(Tool(new ArtistToolProvider(), "get_artist_info"), $$"""{"trackId":"{{TrackId}}"}""", _actions);

        result.Projection["reason"]!.GetValue<string>().ShouldBe(reason);
    }

    [Fact]
    public async Task Export_RefusesOddBitrates_AndUsesTheRangeForTheSize()
    {
        _actions.Tracks.GetTrackAsync(TrackId, Arg.Any<CancellationToken>()).Returns(Dtos.Track(TrackId) with { DurationSeconds = null });
        var tool = Tool(new ExportToolProvider(), "export_audio");

        await Should.ThrowAsync<AgentActionException>(() => InvokeAsync(tool, $$"""{"trackId":"{{TrackId}}","mode":"raw","format":"mp3","mp3BitrateKbps":200}""", _actions));
        await Should.ThrowAsync<AgentActionException>(() => InvokeAsync(tool, $$"""{"trackId":"{{TrackId}}","mode":"raw","format":"flac","bitDepth":20}""", _actions));
        var small = await tool.HeavyWhen!(Invocation(tool, $$$"""{"trackId":"{{{TrackId}}}","mode":"raw","format":"flac","bitDepth":24,"range":{"startSeconds":0,"endSeconds":60}}""", _actions), Token);
        var estimate = await tool.Estimator!(Invocation(tool, $$"""{"trackId":"{{TrackId}}","mode":"raw","format":"mp3","mp3BitrateKbps":192}""", _actions), Token);

        small.ShouldBeFalse();
        estimate.Effect.ShouldBe("Export of about 5 MB to the library");
    }

    [Fact]
    public async Task Exports_WithoutItems_AndListWithAudioFacts()
    {
        _actions.Assets.CreateStemPackAsync(Arg.Any<CreateStemPackRequest>(), Arg.Any<CancellationToken>()).Returns(new StemPackDto([]));
        _actions.Tracks.GetTrackAsync(TrackId, Arg.Any<CancellationToken>()).Returns(Dtos.Track(TrackId));
        _actions.Assets.ListAssetsAsync(Arg.Any<AssetQuery>(), Arg.Any<CancellationToken>()).Returns(new AssetListDto([Dtos.Asset(Id, AssetStatusDto.Ready, 100, null) with { Audio = new AudioFactsDto { Format = AudioFileFormatDto.Mp3, AppliedGainDb = 0, DurationSeconds = 60, PeakDbfs = -1 } }], 1, 1));

        var pack = await InvokeAsync(Tool(new ExportToolProvider(), "export_stem_pack"), $$"""{"trackId":"{{TrackId}}","format":"mp3"}""", _actions);
        var list = await InvokeAsync(Tool(new ExportToolProvider(), "list_exports"), "{}", _actions);
        var stemEstimate = await Tool(new ExportToolProvider(), "export_stem_pack").Estimator!(Invocation(Tool(new ExportToolProvider(), "export_stem_pack"), $$"""{"trackId":"{{TrackId}}","format":"wav"}""", _actions), Token);

        pack.Projection["status"].ShouldBeNull();
        list.Projection[0]!["format"]!.GetValue<string>().ShouldBe("mp3");
        stemEstimate.EstimatedMinutes.ShouldBeNull();
    }

    [Fact]
    public async Task Transcribe_WithoutASuggestion_NeedsAProfile_AndPianoKeepsTheQuality()
    {
        var options = new ScoreOptionsDto
        {
            Source = AudioSourceDto.FromChannel(TrackId, "piano"),
            DurationSeconds = 60,
            GridStatus = GridStatusDto.None,
            DefaultQuality = ScoreQualityDto.Accurate,
            Attribution = "x",
        };
        _actions.Assets.GetScoreOptionsAsync(TrackId, "piano", Arg.Any<CancellationToken>()).Returns(options);
        CreateScoreRequest? sent = null;
        _actions.Assets.CreateScoreAsync(Arg.Do<CreateScoreRequest>(request => sent = request), Arg.Any<CancellationToken>()).Returns(Dtos.Asset(Id, AssetStatusDto.Queued, 0, null));
        _actions.Assets.ListAssetsAsync(Arg.Any<AssetQuery>(), Arg.Any<CancellationToken>()).Returns(new AssetListDto([Dtos.Asset(Id, AssetStatusDto.Ready, 100, null)], 1, 1));
        var tool = Tool(new ScoreToolProvider(), "transcribe_stem");

        await Should.ThrowAsync<AgentActionException>(() => InvokeAsync(tool, $$"""{"trackId":"{{TrackId}}","channel":"piano"}""", _actions));
        var result = await InvokeAsync(tool, $$"""{"trackId":"{{TrackId}}","channel":"piano","profile":"piano","quality":"fast"}""", _actions);
        var list = await InvokeAsync(Tool(new ScoreToolProvider(), "list_scores"), "{}", _actions);

        (sent!.Profile, sent.Quality).ShouldBe((ScoreProfileDto.Piano, ScoreQualityDto.Fast));
        result.Projection["estimatedMinutes"]!.GetValue<double>().ShouldBe(2.0);
        list.Projection[0]!["profile"].ShouldBeNull();
    }

    [Theory]
    [InlineData(GenerationAvailabilityDto.Ready, null)]
    [InlineData(GenerationAvailabilityDto.NotInstalled, "The generator is not installed.")]
    public async Task GenerationStatus_ReadyAndNotInstalled(GenerationAvailabilityDto state, string? reason)
    {
        _actions.Generations.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(new GenerationStatusDto { State = state });

        var result = await InvokeAsync(Tool(new StyleToolProvider(), "get_generation_status"), "{}", _actions);

        result.Projection["reason"]?.GetValue<string>().ShouldBe(reason);
        result.Projection["device"].ShouldBeNull();
    }

    [Fact]
    public async Task Restyle_Defaults_AndRetryOfASong()
    {
        _actions.Generations.CreateRestyleAsync(TrackId, Arg.Any<RestyleRequest>(), Arg.Any<CancellationToken>()).Returns(Gen(GenerationKindDto.Restyle) with { Device = new JobDeviceDto(ProcessingDeviceKindDto.Cpu, null, true, "x") });
        _actions.Generations.RetryGenerationAsync(Id, Arg.Any<CancellationToken>()).Returns(Gen(GenerationKindDto.Song));
        _actions.Generations.ListGenerationsAsync(Arg.Any<GenerationQuery>(), Arg.Any<CancellationToken>()).Returns([Gen(GenerationKindDto.Song)]);

        var restyle = await InvokeAsync(Tool(new StyleToolProvider(), "restyle_track"), $$"""{"trackId":"{{TrackId}}","styleId":"{{Id}}","includedStems":["drums"]}""", _actions);
        var retry = await InvokeAsync(Tool(new StyleToolProvider(), "retry_generation"), $$"""{"generationId":"{{Id}}"}""", _actions);
        var list = await InvokeAsync(Tool(new StyleToolProvider(), "list_generations"), "{}", _actions);

        (restyle.Projection["device"]!.GetValue<string>(), restyle.EstimatedMinutes).ShouldBe(("CPU", 3.0));
        retry.Job!.Phase.ShouldBe(JobPhaseDto.Generation);
        list.Projection[0]!.AsObject().ContainsKey("untrusted_text").ShouldBeTrue();
    }

    [Fact]
    public async Task Song_RefusesOddTimeSignatures_AndUnknownProfilesStillGetACard()
    {
        _actions.Voices.ListVoiceProfilesAsync(Arg.Any<CancellationToken>()).Returns([]);
        _actions.Songs.CreateSongAsync(Arg.Any<SongRequest>(), Arg.Any<CancellationToken>()).Returns(Gen(GenerationKindDto.Song));
        _actions.Songs.ListLyricsSheetsAsync(Arg.Any<CancellationToken>()).Returns([new LyricsSheetSummary { LyricsId = Id, Title = "A", Language = "xx yy", UpdatedAt = Dtos.At }]);
        var convert = Tool(new SongToolProvider(), "convert_take_voice");

        await Should.ThrowAsync<AgentActionException>(() => InvokeAsync(Tool(new SongToolProvider(), "generate_song"), """{"language":"en","leadVoice":"any","timeSignature":5}""", _actions));
        var song = await InvokeAsync(Tool(new SongToolProvider(), "generate_song"), """{"language":"en","leadVoice":"any","instrumental":true}""", _actions);
        var estimate = await convert.Estimator!(Invocation(convert, $$"""{"generationId":"{{Id}}","index":2,"profileId":"{{Id}}"}""", _actions), Token);
        var sheets = await InvokeAsync(Tool(new SongToolProvider(), "list_lyrics_sheets"), "{}", _actions);

        song.EstimatedMinutes.ShouldBe(3.0);
        estimate.Effect.ShouldBe("Convert take 2 to the voice \"unknown\"");
        sheets.Projection[0]!["language"].ShouldBeNull();
    }

    [Fact]
    public async Task ClaudeTest_UnreachableAndHanging_AreReadable()
    {
        var time = new FakeTimeProvider(RunnerHarness.Start);
        var settings = AgentSettings.Defaults(AgentBackendDto.Claude, RunnerHarness.Start);
        var down = new StubHttpHandler { Failure = new HttpRequestException("no route") };
        var refused = new StubHttpHandler().Json("GET /v1/models/claude-opus-5-5", "{}", HttpStatusCode.TooManyRequests);

        var unreachable = await new ClaudeBackendProbe(new ClaudeChatClientFactory(FakeEnvironment.WithKey(), down.Client("https://api.anthropic.com/")), time).TestAsync(settings, Token);
        var limited = await new ClaudeBackendProbe(new ClaudeChatClientFactory(FakeEnvironment.WithKey(), refused.Client("https://api.anthropic.com/")), time).TestAsync(settings, Token);

        unreachable.Detail.ShouldBe(ClaudeErrors.Unreachable);
        limited.Detail.ShouldContain("429");
    }

    private static GenerationDto Gen(GenerationKindDto kind) => new()
    {
        Id = Id,
        Kind = kind,
        Status = GenerationStateDto.Queued,
        Progress = 0,
        CreatedAt = Dtos.At,
        UpdatedAt = Dtos.At,
    };
}
