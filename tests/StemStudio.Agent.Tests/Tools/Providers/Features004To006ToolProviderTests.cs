using System.Text.Json;
using NSubstitute;
using StemStudio.Agent.Ports;
using StemStudio.Agent.Tests.Fixtures;
using StemStudio.Agent.Tools;
using StemStudio.Agent.Tools.Providers;
using StemStudio.Contracts.Agent;
using StemStudio.Contracts.Artists;
using StemStudio.Contracts.Lyrics;
using StemStudio.Contracts.Tracks;
using static StemStudio.Agent.Tests.Tools.Providers.ProviderTestKit;

namespace StemStudio.Agent.Tests.Tools.Providers;

/// <summary>T075: help (004), artist info (005) and lyrics (006) tools (FR-024, FR-026).</summary>
public sealed class Features004To006ToolProviderTests
{
    private static readonly Guid TrackId = Guid.NewGuid();
    private readonly IStemStudioActions _actions = Actions();

    private static string TrackArgs => $$"""{"trackId":"{{TrackId}}"}""";

    [Fact]
    public void Tools_MatchTheCatalogue()
    {
        MatchesCatalogue(new HelpToolProvider(), "004");
        MatchesCatalogue(new ArtistToolProvider(), "005");
        MatchesCatalogue(new LyricsToolProvider(), "006");
    }

    [Fact]
    public void HelpSchemas_ValidateQueryTopicAndPage()
    {
        var help = new HelpToolProvider();
        Valid(Tool(help, "search_help"), """{"query":"loop"}""").ShouldBeTrue();
        Valid(Tool(help, "search_help"), """{"query":""}""").ShouldBeFalse();
        Valid(Tool(help, "open_help"), """{"topicId":"mixer-loop"}""").ShouldBeTrue();
        Valid(Tool(help, "open_help"), """{"topicId":"../etc"}""").ShouldBeFalse();
        Valid(Tool(help, "navigate"), """{"page":"settings"}""").ShouldBeTrue();
        Valid(Tool(help, "navigate"), """{"page":"admin"}""").ShouldBeFalse();
    }

    [Fact]
    public void SearchHelp_KeepsFiveTopicsWithShortSummaries()
    {
        var topics = Enumerable.Range(1, 8).Select(i => new { topicId = $"t{i}", title = $"Topic {i}", summary = new string('x', 400), extra = "drop" });

        var result = Tool(new HelpToolProvider(), "search_help").MapUiResult!(Element("{}"), Element(JsonSerializer.Serialize(topics)));

        var items = result.Projection.AsArray();
        items.Count.ShouldBe(5);
        items[0]!["summary"]!.GetValue<string>().Length.ShouldBe(300);
        items[0]!.AsObject().Select(property => property.Key).ShouldBe(["topicId", "title", "summary"]);
    }

    [Fact]
    public async Task ArtistInfo_PutsExternalTextInUntrustedFields_AndTaints()
    {
        _actions.Artists.GetBandInfoAsync(TrackId, Arg.Any<CancellationToken>()).Returns(BandInfo());

        var result = await InvokeAsync(Tool(new ArtistToolProvider(), "get_artist_info"), TrackArgs, _actions);

        var projected = ResultProjector.Project(result.Projection);
        projected.Tainted.ShouldBeTrue();
        var json = JsonDocument.Parse(projected.Json).RootElement;
        json.GetProperty("status").GetString().ShouldBe("found");
        json.GetProperty("artist").GetProperty("untrusted_text").GetString().ShouldBe("Metallica; genres: thrash metal, heavy metal; members: James Hetfield, Lars Ulrich");
        json.GetProperty("artist").GetProperty("beginYear").GetInt32().ShouldBe(1981);
        json.GetProperty("untrusted_text").GetString()!.Length.ShouldBeLessThanOrEqualTo(500);
        json.GetProperty("source").GetString().ShouldBe("Wikipedia (CC BY-SA 4.0)");
        projected.Json.ShouldNotContain("en.wikipedia.org");
    }

    [Fact]
    public async Task ArtistInfo_ForClaude_OmitsTheSummary()
    {
        _actions.Artists.GetBandInfoAsync(TrackId, Arg.Any<CancellationToken>()).Returns(BandInfo());

        var result = await InvokeAsync(Tool(new ArtistToolProvider(), "get_artist_info"), TrackArgs, _actions, backend: AgentBackendDto.Claude);

        result.Projection.AsObject().ContainsKey("untrusted_text").ShouldBeFalse();
        ResultProjector.Project(result.Projection).Tainted.ShouldBeTrue();
    }

    [Fact]
    public async Task ArtistInfo_NotFound_HasNoExternalText()
    {
        _actions.Artists.GetBandInfoAsync(TrackId, Arg.Any<CancellationToken>()).Returns(new BandInfoDto { Status = MetadataStatusDto.NotFound, Reason = "No match" });

        var result = await InvokeAsync(Tool(new ArtistToolProvider(), "get_artist_info"), TrackArgs, _actions);

        Json(result.Projection).ShouldBe("""{"status":"notFound","reason":"No match","artist":null,"recording":null}""");
    }

    [Fact]
    public async Task RefreshArtwork_RetriesTheLookup()
    {
        _actions.Artists.LookupMetadataAsync(TrackId, Arg.Any<CancellationToken>()).Returns(Dtos.Track(TrackId));

        var result = await InvokeAsync(Tool(new ArtistToolProvider(), "refresh_artwork"), TrackArgs, _actions);

        result.Projection.AsObject().ContainsKey("metadataStatus").ShouldBeTrue();
    }

    [Fact]
    public async Task LyricsStatus_NeverCarriesText()
    {
        _actions.Lyrics.GetLyricsSummaryAsync(TrackId, Arg.Any<CancellationToken>()).Returns(new LyricsSummary { Status = LyricsStatusDto.Found, SyncLevel = SyncLevelDto.Line, Instrumental = false });

        var result = await InvokeAsync(Tool(new LyricsToolProvider(), "get_lyrics_status"), TrackArgs, _actions);

        Json(result.Projection).ShouldBe("""{"status":"found","syncLevel":"line","origin":null,"source":null,"instrumental":false,"reason":null}""");
    }

    [Fact]
    public async Task FetchLyrics_IsAJobWithARemoveCompensation()
    {
        _actions.Lyrics.SearchLyricsAsync(TrackId, Arg.Any<CancellationToken>()).Returns(new LyricsSummary { Status = LyricsStatusDto.Searching, Instrumental = false });

        var result = await InvokeAsync(Tool(new LyricsToolProvider(), "fetch_lyrics"), TrackArgs, _actions);

        (result.Job!.Phase, result.Job.TrackId).ShouldBe((JobPhaseDto.Lyrics, TrackId));
        (result.Undo!.Tool, result.Undo.Arguments).ShouldBe(("remove_lyrics", TrackArgs));
        result.Projection["jobRef"]!["phase"]!.GetValue<string>().ShouldBe("lyrics");
    }

    [Fact]
    public async Task AlignLyrics_IsHeavy_WithAnEstimate()
    {
        _actions.Tracks.GetTrackAsync(TrackId, Arg.Any<CancellationToken>()).Returns(Dtos.Track(TrackId) with { DurationSeconds = 240 });
        _actions.Lyrics.AlignLyricsAsync(TrackId, Arg.Any<CancellationToken>()).Returns(new LyricsSummary { Status = LyricsStatusDto.Aligning, Instrumental = false });
        var tool = Tool(new LyricsToolProvider(), "align_lyrics");

        var estimate = await tool.Estimator!(Invocation(tool, TrackArgs, _actions), TestContext.Current.CancellationToken);
        var result = await InvokeAsync(tool, TrackArgs, _actions);

        (estimate.EstimatedMinutes, estimate.Device).ShouldBe((2.0, "GPU"));
        result.Projection["estimatedMinutes"]!.GetValue<double>().ShouldBe(2.0);
        tool.TargetArgument.ShouldBe("trackId");
    }

    [Fact]
    public void ShowKaraoke_UndoesToThePreviousState()
    {
        Valid(Tool(new LyricsToolProvider(), "show_karaoke"), """{"enabled":true}""").ShouldBeTrue();

        var undo = UiUndo.From("show_karaoke", Element("""{"previous":false,"current":true}"""));

        undo.Select(action => (action.Tool, action.Arguments)).ShouldBe([("show_karaoke", """{"enabled":false}""")]);
    }

    [Fact]
    public async Task RemoveLyrics_IsAnUndoOnlyTool()
    {
        var remove = UndoTools.All.Single(tool => tool.Name == "remove_lyrics");
        _actions.Lyrics.RemoveLyricsAsync(TrackId, Arg.Any<CancellationToken>()).Returns(new LyricsSummary { Status = LyricsStatusDto.None, Instrumental = false });

        var result = await InvokeAsync(remove, TrackArgs, _actions);

        result.Projection["status"]!.GetValue<string>().ShouldBe("none");
        new LyricsToolProvider().GetTools().ShouldNotContain(tool => tool.Name == "remove_lyrics");
    }

    private static BandInfoDto BandInfo() => new()
    {
        Status = MetadataStatusDto.Found,
        Recording = new RecordingInfoDto("Enter Sandman", "Metallica", 1991),
        Artist = new ArtistInfoDto
        {
            Mbid = Guid.NewGuid(),
            Name = "Metallica",
            Type = ArtistTypeDto.Group,
            Country = "US",
            BeginYear = 1981,
            Genres = ["thrash metal", "heavy metal"],
            Members = [new ArtistMemberDto("James Hetfield"), new ArtistMemberDto("Lars Ulrich")],
            Summary = new WikipediaSummaryDto { Language = "en", Title = "Metallica", Extract = new string('m', 900), PageUrl = "https://en.wikipedia.org/wiki/Metallica", HistoryUrl = "https://en.wikipedia.org/w/index.php?title=Metallica&action=history" },
        },
    };
}
