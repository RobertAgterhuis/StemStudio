using System.Text.Json;
using StemStudio.Agent.Model;
using StemStudio.Agent.Ports;
using StemStudio.AgentEval.Fixture;
using StemStudio.Contracts.Agent;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Generation;
using StemStudio.Contracts.Lyrics;
using StemStudio.Contracts.Scores;
using StemStudio.Contracts.Styles;

namespace StemStudio.AgentEval.Tests;

/// <summary>The seeded fixture: reads return it, writes change it, missing items are 404s; the page answers like the frontend.</summary>
public sealed class FixtureTests
{
    private readonly FixtureLibrary _library = FixtureLibrary.Seed(Repo.Presets);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private FixtureActions Actions => new(_library);

    private Guid Id(string key) => _library.Ids.Of(key);

    private static async Task NotFoundAsync(Func<Task> call) =>
        (await Should.ThrowAsync<AgentActionException>(call)).IsNotFound.ShouldBeTrue();

    [Fact]
    public async Task Tracks_ImportRetryCancelDeleteAndTheDevice()
    {
        var tracks = Actions.Tracks;

        (await tracks.ImportFromUrlAsync("https://youtu.be/dQw4w9WgXcQ", Token)).SourceUrl.ShouldBe("https://youtu.be/dQw4w9WgXcQ");
        (await tracks.RetryTrackAsync(Id("t-fail"), Token)).Id.ShouldBe(Id("t-fail"));
        (await tracks.CancelDetailAsync(Id("t-hero"), Token)).Detail!.Status.ShouldBe(StemStudio.Contracts.Tracks.DetailPhaseStatusDto.Cancelled);
        (await tracks.GetDeviceAsync(Token)).Reachable.ShouldBeTrue();
        await tracks.DeleteTrackAsync(Id("t-enter"), Token);
        await NotFoundAsync(() => tracks.DeleteTrackAsync(Id("t-enter"), Token));
        await NotFoundAsync(() => tracks.GetTrackAsync(Guid.NewGuid(), Token));
    }

    [Theory]
    [InlineData("https://evil.example/x")]
    [InlineData("ftp://www.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("not a url")]
    public async Task Tracks_ImportRejectsAUrlOutsideTheAllowListLikeTheApi(string url)
    {
        var count = _library.Tracks.Count;

        var problem = await Should.ThrowAsync<AgentActionException>(() => Actions.Tracks.ImportFromUrlAsync(url, Token));

        problem.StatusCode.ShouldBe(400);
        problem.ProblemCode.ShouldBe("invalid-request");
        problem.Message.ShouldStartWith("One or more validation errors occurred. url: ");
        _library.Tracks.Count.ShouldBe(count);
    }

    [Theory]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://music.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://m.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("http://youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://open.spotify.com/track/4uLU6hMCjMI75M1A2tKUQC")]
    public async Task Tracks_ImportAcceptsTheAllowedHosts(string url) =>
        (await Actions.Tracks.ImportFromUrlAsync(url, Token)).SourceUrl.ShouldBe(url);

    [Fact]
    public async Task Mixes_DuplicateDeleteAndTheSummaryOfADeletedTrack()
    {
        var mixes = Actions.Mixes;
        var copy = await mixes.DuplicateMixAsync(Id("m-hero-practice"), Token);

        copy.Settings.Name.ShouldEndWith(" (copy)");
        _library.Tracks.Remove(copy.TrackId);
        (await mixes.ListMixesAsync(null, Token)).ShouldContain(mix => mix.TrackTitle == "(deleted)");
        await mixes.DeleteMixAsync(copy.Id, Token);
        await NotFoundAsync(() => mixes.DeleteMixAsync(copy.Id, Token));
    }

    [Fact]
    public async Task ArtistsAndLyrics_AnswerWithoutExternalLookups()
    {
        var actions = Actions;

        (await actions.Artists.GetBandInfoAsync(Id("t-hero"), Token)).Reason.ShouldNotBeNull();
        (await actions.Artists.LookupMetadataAsync(Id("t-hero"), Token)).Id.ShouldBe(Id("t-hero"));
        (await actions.Lyrics.GetLyricsSummaryAsync(Id("t-hero"), Token)).Status.ShouldBe(LyricsStatusDto.None);
        (await actions.Lyrics.SearchLyricsAsync(Id("t-hero"), Token)).Status.ShouldBe(LyricsStatusDto.Found);
        (await actions.Lyrics.AlignLyricsAsync(Id("t-hero"), Token)).Status.ShouldBe(LyricsStatusDto.Found);
        (await actions.Lyrics.RemoveLyricsAsync(Id("t-hero"), Token)).Status.ShouldBe(LyricsStatusDto.None);
    }

    [Fact]
    public async Task Assets_ListByKind_StemPack_Scores_AndDelete()
    {
        var assets = Actions.Assets;
        var export = await assets.CreateAudioExportAsync(new CreateAudioExportRequest { TrackId = Id("t-hero"), Mode = default, Format = new ExportFormatDto(default) }, Token);
        var pack = await assets.CreateStemPackAsync(new CreateStemPackRequest(Id("t-hero"), new ExportFormatDto(default)), Token);
        var score = await assets.CreateScoreAsync(new CreateScoreRequest { Source = AudioSourceDto.FromChannel(Id("t-hero"), "piano"), Name = "Piano" }, Token);

        (export.Name, pack.Items.Count, score.Kind).ShouldBe(("Export", 1, AssetKindDto.Score));
        (await assets.ListAssetsAsync(new AssetQuery(), Token)).Items.Count.ShouldBe(3);
        (await assets.ListAssetsAsync(new AssetQuery { Kind = AssetKindDto.Score }, Token)).Items.Count.ShouldBe(1);
        (await assets.GetAssetAsync(score.Id, Token)).ShouldBe(score);
        (await assets.GetScoreOptionsAsync(Id("t-hero"), "piano", Token)).SuggestedProfile.ShouldBe(ScoreProfileDto.Piano);
        (await assets.GetScoreOptionsAsync(Id("t-hero"), "nonsense", Token)).SuggestedProfile.ShouldBeNull();
        await assets.DeleteAssetAsync(score.Id, Token);
        await NotFoundAsync(() => assets.DeleteAssetAsync(score.Id, Token));
        await NotFoundAsync(() => assets.CreateScoreAsync(new CreateScoreRequest { Source = AudioSourceDto.FromAsset(Guid.NewGuid()) }, Token));
    }

    [Fact]
    public async Task Styles_FilterCreateAndDelete()
    {
        var styles = Actions.Styles;
        var all = await styles.ListStylesAsync(new StyleQuery(), Token);
        var created = await styles.CreateStyleAsync(new StyleInput { Name = "Mine", GenreFamily = default, TempoFeel = default, VocalHandling = default, DefaultStrength = default }, Token);

        (await styles.ListStylesAsync(new StyleQuery { Kind = StyleKindDto.User }, Token)).ShouldBe([created]);
        (await styles.ListStylesAsync(new StyleQuery { Query = "reggae" }, Token)).ShouldNotBeEmpty();
        (await styles.ListStylesAsync(new StyleQuery { Query = "  ", GenreFamily = all[0].GenreFamily }, Token)).ShouldContain(all[0]);
        await styles.DeleteStyleAsync(created.Id, Token);
        await NotFoundAsync(() => styles.DeleteStyleAsync(created.Id, Token));
    }

    [Fact]
    public async Task Generations_Restyle_Songs_Takes_AndVoices()
    {
        var actions = Actions;
        var restyle = await actions.Generations.CreateRestyleAsync(Id("t-hero"), new RestyleRequest { StyleId = Guid.NewGuid(), Mode = default }, Token);
        var song = await actions.Songs.CreateSongAsync(new SongRequest { Language = "nl", LeadVoice = default }, Token);

        (await actions.Generations.GetStatusAsync(Token)).State.ShouldBe(GenerationAvailabilityDto.Ready);
        (await actions.Generations.ListGenerationsAsync(new GenerationQuery { Kind = GenerationKindDto.Restyle }, Token)).ShouldContain(restyle);
        (await actions.Generations.ListGenerationsAsync(new GenerationQuery(), Token)).ShouldContain(song);
        (await actions.Generations.CancelGenerationAsync(restyle.Id, Token)).ShouldBe(restyle);
        (await actions.Generations.RetryGenerationAsync(restyle.Id, Token)).ShouldBe(restyle);
        (await actions.Generations.ImportTakeAsync(restyle.Id, 2, Token)).Title.ShouldBe("Take 2");
        await actions.Generations.DeleteGenerationAsync(restyle.Id, Token);
        await NotFoundAsync(() => actions.Generations.DeleteGenerationAsync(restyle.Id, Token));
        var missing = await Should.ThrowAsync<AgentActionException>(() => actions.Voices.CreateVoiceConversionAsync(song.Id, 1, new VoiceConversionRequest { ProfileId = Guid.NewGuid() }, Token));
        missing.StatusCode.ShouldBe(409);
    }

    [Fact]
    public async Task LyricsSheets_CreateListAndDelete()
    {
        var songs = Actions.Songs;
        var sheet = await songs.CreateLyricsSheetAsync(new LyricsInput { Title = "Zomer", Language = "nl", Text = "la la" }, Token);

        (await songs.ListLyricsSheetsAsync(Token)).ShouldBe([sheet]);
        await songs.DeleteLyricsSheetAsync(sheet.LyricsId, Token);
        await NotFoundAsync(() => songs.DeleteLyricsSheetAsync(sheet.LyricsId, Token));
    }

    [Fact]
    public async Task MemoryStores_ListDeleteAndTheFixedSettings()
    {
        var conversations = new MemoryConversationStore();
        var conversation = new Conversation { Id = Guid.NewGuid(), Title = "c", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        await conversations.CreateAsync(conversation, Token);

        (await conversations.ListAsync(10, null, Token)).ShouldBe([conversation]);
        (await conversations.DeleteAsync(conversation.Id, Token)).ShouldBeTrue();
        (await new MemoryRunStore().GetActiveRunAsync(Token)).ShouldBeNull();
        var settings = new FixedSettingsStore(AgentSettings.Defaults(AgentBackendDto.Ollama, DateTimeOffset.UtcNow));
        await settings.SaveAsync(await settings.GetAsync(Token), Token);
        new IdleGpu().IsHeavyJobActive().ShouldBeFalse();
    }

    [Fact]
    public async Task Page_WithoutAnOpenMixer_FailsMixerTools_ButNavigates()
    {
        var page = new FixturePage(null, null);

        (await Ui(page, "mixer_get_state")).Ok.ShouldBeFalse();
        (await Ui(page, "navigate", """{"page":"tracks"}""")).Ok.ShouldBeTrue();
        page.TryComplete(Guid.NewGuid(), Guid.NewGuid(), new UiToolResult { Ok = true }).ShouldBeFalse();
    }

    [Fact]
    public async Task Page_LoopMetronomeKaraokeAndUnknownTools_AnswerPreviousAndCurrent()
    {
        var page = new FixturePage(Id("t-hero"), Id("m-hero-practice"));

        Json(await Ui(page, "set_loop", """{"startBar":3,"endBar":5}""")).GetProperty("current").GetProperty("end").GetDouble().ShouldBe(10);
        Json(await Ui(page, "set_loop", """{"enabled":false}""")).GetProperty("current").GetProperty("end").GetDouble().ShouldBe(8);
        Json(await Ui(page, "set_metronome", """{"enabled":true,"volume":0.5,"beatsPerBar":3,"offsetMs":20}""")).GetProperty("current").GetProperty("beatsPerBar").GetInt32().ShouldBe(3);
        Json(await Ui(page, "show_karaoke")).GetProperty("current").GetBoolean().ShouldBeTrue();
        Json(await Ui(page, "mixer_set_channel", """{"stem":"other","gainChangeDb":-3,"mute":true,"solo":false,"pan":0.5}""")).GetProperty("current").GetProperty("gainDb").GetDouble().ShouldBe(-3);
        Json(await Ui(page, "mixer_get_state")).GetProperty("mixId").GetString().ShouldBe(Id("m-hero-practice").ToString());
        (await Ui(page, "mixer_play")).Ok.ShouldBeTrue();
        page.IsRestored.ShouldBeFalse();
    }

    private static Task<UiToolResult> Ui(FixturePage page, string tool, string arguments = "{}") =>
        page.RequestAsync(new UiToolRequest { RunId = Guid.NewGuid(), StepId = Guid.NewGuid(), Tool = tool, Arguments = arguments, Timeout = TimeSpan.FromSeconds(1) }, Token);

    private static JsonElement Json(UiToolResult result) => JsonDocument.Parse(result.Result!).RootElement;
}
