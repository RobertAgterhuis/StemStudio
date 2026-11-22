using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using StemStudio.Api.Scores;
using StemStudio.Api.Tests.Assets;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Worker;
using StemStudio.Contracts;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.InstrumentViews;
using StemStudio.Contracts.Scores;
using StemStudio.Domain.Scores;
using static StemStudio.Api.Tests.Scores.ScoreTestData;

namespace StemStudio.Api.Tests.Scores;

/// <summary>Feature 016 T079: tablature validation and options, single drum pieces and the <c>channel</c> filter of the library (FR-020..FR-023).</summary>
public sealed class TablatureScoreTests : ApiTestBase
{
    public static TheoryData<string, ScoreProfileDto, ScoreTablatureDto, string> InvalidCases => new()
    {
        { "vocals", ScoreProfileDto.Keys, Settings(TuningIdDto.GuitarStandard), "tablature-not-allowed" },
        { "guitar", ScoreProfileDto.Guitar, Settings(TuningIdDto.Bass4Standard), "tuning-mismatch" },
        { "bass", ScoreProfileDto.Bass, Settings(TuningIdDto.Bass4Standard, 2), "capo-not-allowed" },
    };

    public static TheoryData<string, ScoreProfileDto, (bool Available, TuningIdDto? DefaultTuning, bool Capo, int Tunings)> OptionCases => new()
    {
        { "guitar", ScoreProfileDto.Guitar, (true, TuningIdDto.GuitarStandard, true, 7) },
        { "bass", ScoreProfileDto.Bass, (true, TuningIdDto.Bass4Standard, false, 5) },
        { "vocals", ScoreProfileDto.Vocals, (false, null, false, 0) },
        { "piano", ScoreProfileDto.Piano, (false, null, false, 0) },
    };

    private static ScoreTablatureDto Settings(TuningIdDto tuning, int capo = 0) => new() { Tuning = tuning, Capo = capo };

    private static CreateScoreRequest Tab(Guid trackId, string channel, ScoreProfileDto? profile, ScoreTablatureDto tablature) =>
        Request(trackId, channel) with { Profile = profile, Tablature = tablature };

    [Theory]
    [MemberData(nameof(InvalidCases))]
    public async Task InvalidTablature_Is422WithATypedProblem(string channel, ScoreProfileDto profile, ScoreTablatureDto tablature, string type)
    {
        var track = await Factory.SeedAsync(AssetTestData.NamedTrack());

        using var response = await Client.PostScoreAsync(Tab(track.Id, channel, profile, tablature));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        await ContractAssert.MatchesAsync(response, "createScore");
        (await response.JsonAsync()).GetProperty("type").GetString().ShouldBe(type);
        Worker.TranscriptionCreates.ShouldBeEmpty();
    }

    [Fact]
    public async Task ACapoOutsideTheRange_IsAFieldError()
    {
        var track = await Factory.SeedAsync(AssetTestData.NamedTrack());

        using var response = await Client.PostScoreAsync(Tab(track.Id, "guitar", null, Settings(TuningIdDto.GuitarStandard, 13)));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.JsonAsync()).GetProperty("errors").TryGetProperty("tablature.capo", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task ValidTablature_IsSentToTheWorkerAndKeptInTheRecipe()
    {
        var track = await Factory.SeedAsync(AssetTestData.NamedTrack());

        using var response = await Client.PostScoreAsync(Tab(track.Id, "guitar", null, Settings(TuningIdDto.GuitarDropD, 2)));

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        Worker.TranscriptionCreates.Single().Tablature!.Capo.ShouldBe(2);
        var recipe = (await response.BodyAsync<AssetDto>()).Provenance.ScoreRecipe!;
        (recipe.Tablature!.Tuning, recipe.Tablature.Capo).ShouldBe((TuningIdDto.GuitarDropD, 2));
    }

    [Fact]
    public async Task TheFacts_ReportTheTablatureSummary()
    {
        var track = await Factory.SeedAsync(AssetTestData.NamedTrack());
        using var created = await Client.PostScoreAsync(Tab(track.Id, "guitar", null, Settings(TuningIdDto.GuitarDropD, 2)));
        var asset = await created.BodyAsync<AssetDto>();
        var summary = new WorkerTablatureSummary { Tuning = "guitar-drop-d", Strings = 6, Capo = 2, Layout = "notation-and-tab", OctaveMoved = 1, NotesDropped = 0, WideStretches = 3 };
        CompleteTranscriptions(Worker, Result(Summary(ScoreProfile.Guitar) with { Tablature = summary }));

        await Factory.SyncAssetsAsync();

        var ready = await Client.GetFromJsonAsync<AssetDto>(ApiRoutes.Asset(asset.Id), ApiFactory.Json, Ct);
        var facts = ready!.Score!.Tablature!;
        (facts.Tuning, facts.Strings, facts.Capo, facts.OctaveMoved, facts.WideStretches).ShouldBe((TuningIdDto.GuitarDropD, 6, 2, 1, 3));
    }

    [Theory]
    [MemberData(nameof(OptionCases))]
    public async Task Options_OfferTablatureOnlyForTheSuggestedGuitarOrBass(string channel, ScoreProfileDto suggested, (bool Available, TuningIdDto? DefaultTuning, bool Capo, int Tunings) expected)
    {
        var track = await Factory.SeedAsync(AssetTestData.NamedTrack());

        using var response = await Client.GetAsync(new Uri(ApiRoutes.ScoreOptionsForChannel(track.Id, channel), UriKind.Relative), Ct);

        await ContractAssert.MatchesAsync(response, "getScoreOptions");
        var options = await response.BodyAsync<ScoreOptionsDto>();
        options.SuggestedProfile.ShouldBe(suggested);
        var tablature = options.Tablature!;
        (tablature.Available, tablature.DefaultTuning, tablature.CapoAllowed, tablature.Tunings.Count).ShouldBe(expected);
    }

    [Fact]
    public async Task ASingleDrumPiece_SendsOnePieceAndTheSinglePiece()
    {
        var track = await Factory.SeedAsync(DrumsSplitTrack());

        using var options = await Client.GetAsync(new Uri(ApiRoutes.ScoreOptionsForChannel(track.Id, "drums.kick"), UriKind.Relative), Ct);
        using var response = await Client.PostScoreAsync(Request(track.Id, "drums.kick") with { Profile = ScoreProfileDto.Drums });

        options.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await options.BodyAsync<ScoreOptionsDto>()).AllowedProfiles.ShouldBe([ScoreProfileDto.Drums]);
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var sent = Worker.TranscriptionCreates.Single();
        sent.SinglePiece.ShouldBe("kick");
        sent.DrumInputs.ShouldBe(new WorkerDrumInputs($"tracks/{track.Id:D}/detail/drums/kick.flac", null, null, null, null));
        sent.Input.ShouldBeNull();
    }

    [Fact]
    public async Task TheWholeDrumsChannel_DoesNotSetASinglePiece()
    {
        var track = await Factory.SeedAsync(DrumsSplitTrack());

        await Client.PostScoreAsync(Request(track.Id, "drums"));

        Worker.TranscriptionCreates.Single().SinglePiece.ShouldBeNull();
    }

    [Fact]
    public async Task ListAssets_FiltersOnTheChannelWithTheTrack_NewestFirst()
    {
        var track = await Factory.SeedAsync(DrumsSplitTrack());
        var ids = new List<Guid>();
        foreach (var channel in new[] { "bass", "drums.kick", "bass" })
        {
            using var created = await Client.PostScoreAsync(Request(track.Id, channel) with { Profile = channel == "bass" ? ScoreProfileDto.Bass : ScoreProfileDto.Drums });
            ids.Add((await created.BodyAsync<AssetDto>()).Id);
            Factory.Clock.Advance(TimeSpan.FromMinutes(1));
        }

        using var response = await Client.GetAsync(new Uri($"{ApiRoutes.Assets}?trackId={track.Id}&channel=bass", UriKind.Relative), Ct);
        using var kick = await Client.GetAsync(new Uri($"{ApiRoutes.Assets}?trackId={track.Id}&channel=drums.kick", UriKind.Relative), Ct);
        using var none = await Client.GetAsync(new Uri($"{ApiRoutes.Assets}?trackId={track.Id}&channel=vocals", UriKind.Relative), Ct);

        await ContractAssert.MatchesAsync(response, "listAssets");
        (await response.BodyAsync<AssetListDto>()).Items.Select(item => item.Id).ShouldBe([ids[2], ids[0]]);
        (await kick.BodyAsync<AssetListDto>()).Items.Select(item => item.Id).ShouldBe([ids[1]]);
        (await none.BodyAsync<AssetListDto>()).Items.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("BASS")]
    [InlineData("drums.")]
    [InlineData("a.b.c")]
    [InlineData("bass'--")]
    public async Task ListAssets_AMalformedChannelIs422(string channel)
    {
        using var response = await Client.GetAsync(new Uri($"{ApiRoutes.Assets}?trackId={Guid.NewGuid()}&channel={Uri.EscapeDataString(channel)}", UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.JsonAsync()).GetProperty("errors").TryGetProperty("channel", out _).ShouldBeTrue();
    }

    [Theory]
    [InlineData(ScoreUnprocessable.TablatureNotAllowed, "tablature-not-allowed")]
    [InlineData(ScoreUnprocessable.TuningMismatch, "tuning-mismatch")]
    [InlineData(ScoreUnprocessable.CapoNotAllowed, "capo-not-allowed")]
    [InlineData(ScoreUnprocessable.ClefNotAllowed, "clef-not-allowed")]
    [InlineData(ScoreUnprocessable.PartNotAllowed, "part-not-allowed")]
    public void EveryUnprocessableCode_HasItsProblemType(ScoreUnprocessable code, string type)
    {
        var problem = ScoreProblems.For(new ScoreUnprocessableException(code, "detail")).ShouldBeOfType<ProblemHttpResult>();

        (problem.StatusCode, problem.ProblemDetails.Type, problem.ProblemDetails.Detail).ShouldBe((422, type, "detail"));
    }

    [Fact]
    public async Task ListAssets_ChannelWithoutATrack_IsRefused()
    {
        using var response = await Client.GetAsync(new Uri($"{ApiRoutes.Assets}?channel=bass", UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }
}
