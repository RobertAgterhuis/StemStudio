using System.Net;
using StemStudio.Api.Tests.Assets;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Worker;
using StemStudio.Contracts;
using StemStudio.Contracts.Scores;
using StemStudio.Domain.Scores;
using StemStudio.Domain.Tracks.Detail;
using static StemStudio.Api.Tests.Scores.ScoreTestData;

namespace StemStudio.Api.Tests.Scores;

/// <summary>Feature 008 T061: drum notation needs the split drums (FR-009; data-model 4.2, 4.4).</summary>
public sealed class DrumScoreTests : ApiTestBase
{
    [Fact]
    public async Task Drums_BeforeTheSplitAre409DrumsNotSplit()
    {
        var track = await Factory.SeedAsync(AssetTestData.NamedTrack());

        using var options = await Client.GetAsync(new Uri(ApiRoutes.ScoreOptionsForChannel(track.Id, "drums"), UriKind.Relative), Ct);
        using var create = await Client.PostScoreAsync(Request(track.Id, "drums"));

        await ContractAssert.MatchesAsync(options, "getScoreOptions");
        var dto = await options.BodyAsync<ScoreOptionsDto>();
        (dto.SuggestedProfile, dto.DrumsRequireSplit, dto.AllowedProfiles.Single()).ShouldBe((ScoreProfileDto.Drums, true, ScoreProfileDto.Drums));
        dto.Estimates.ShouldBe([new ScoreEstimateDto(ScoreProfileDto.Drums, null, 9)]);
        create.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        await ContractAssert.MatchesAsync(create, "createScore");
        (await create.JsonAsync()).GetProperty("type").GetString().ShouldBe("drums-not-split");
    }

    [Fact]
    public async Task Drums_AfterTheSplitSendTheFiveDetailPaths()
    {
        var track = await Factory.SeedAsync(DrumsSplitTrack());

        using var response = await Client.PostScoreAsync(Request(track.Id, "drums"));

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var sent = Worker.TranscriptionCreates.Single();
        string Piece(string name) => $"tracks/{track.Id:D}/detail/drums/{name}.flac";
        (sent.Profile, sent.Input, sent.Resolution).ShouldBe((ScoreProfile.Drums, null, ScoreResolution.Sixteenth));
        sent.DrumInputs.ShouldBe(new WorkerDrumInputs(Piece("kick"), Piece("snare"), Piece("toms"), Piece("hihat"), Piece("cymbals")));
        sent.Titles.InstrumentName.ShouldBe("Drums");
        (await (await Client.GetAsync(new Uri(ApiRoutes.ScoreOptionsForChannel(track.Id, "drums"), UriKind.Relative), Ct)).BodyAsync<ScoreOptionsDto>()).DrumsRequireSplit.ShouldBeFalse();
    }

    [Fact]
    public async Task MissingPieces_AreOmitted()
    {
        var seeded = AssetTestData.NamedTrack();
        seeded.RequestDetail(SyncTestData.T0);
        seeded.AssignDetailJob(SyncTestData.DetailJobId, SyncTestData.T0);
        seeded.CompleteDetail(
        [
            new ParentOutcome("drums", ParentOutcomeKind.Split, [new("kick", 8), new("snare", 8), new("rest", 8)]),
            new ParentOutcome("vocals", ParentOutcomeKind.NothingFurther, []),
            new ParentOutcome("bass", ParentOutcomeKind.NotSupported, []),
            new ParentOutcome("guitar", ParentOutcomeKind.NothingFurther, []),
            new ParentOutcome("piano", ParentOutcomeKind.SkippedSilent, []),
            new ParentOutcome("other", ParentOutcomeKind.NotReliable, []),
        ],
            SyncTestData.T0);
        var track = await Factory.SeedAsync(seeded);

        await Client.PostScoreAsync(Request(track.Id, "drums"));

        var inputs = Worker.TranscriptionCreates.Single().DrumInputs!;
        (inputs.Kick is not null, inputs.Snare is not null, inputs.Toms, inputs.Hihat, inputs.Cymbals).ShouldBe((true, true, null, null, null));
    }

    [Theory]
    [InlineData("drums.kick")]
    [InlineData("bass")]
    public async Task OtherChannels_CannotUseTheDrumsProfile(string channel)
    {
        var track = await Factory.SeedAsync(DrumsSplitTrack());

        using var response = await Client.PostScoreAsync(Request(track.Id, channel) with { Profile = ScoreProfileDto.Drums });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.FieldErrorsAsync("profile")).ShouldNotBeEmpty();
    }
}
