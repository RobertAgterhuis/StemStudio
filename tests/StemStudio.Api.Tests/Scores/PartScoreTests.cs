using System.Net;
using System.Net.Http.Json;
using StemStudio.Api.Tests.Assets;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Worker;
using StemStudio.Contracts;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.InstrumentViews;
using StemStudio.Contracts.Scores;
using StemStudio.Domain.Scores;
using StemStudio.Domain.Tracks;
using StemStudio.Domain.Tracks.Detail;
using static StemStudio.Api.Tests.Scores.ScoreTestData;

namespace StemStudio.Api.Tests.Scores;

/// <summary>Feature 016 T108: section profiles, written-instrument parts and clefs, <c>ScoreOptions.parts</c> and <c>ScoreFacts.part</c> (FR-050..FR-053).</summary>
public sealed class PartScoreTests : ApiTestBase
{
    public static TheoryData<string, ScoreProfileDto, (bool Available, bool ClefAllowed)> PartOptionCases => new()
    {
        { "vocals", ScoreProfileDto.Vocals, (true, true) },
        { "bass", ScoreProfileDto.Bass, (true, true) },
        { "piano", ScoreProfileDto.Piano, (true, false) },
        { "other", ScoreProfileDto.Keys, (true, false) },
        { "other.brass", ScoreProfileDto.Brass, (true, true) },
        { "drums", ScoreProfileDto.Drums, (false, false) },
    };

    private static Track SectionTrack()
    {
        var track = AssetTestData.NamedTrack();
        track.RequestDetail(SyncTestData.T0);
        track.AssignDetailJob(SyncTestData.DetailJobId, SyncTestData.T0);
        track.CompleteDetail(
        [
            new ParentOutcome("drums", ParentOutcomeKind.Split, [new("kick", 8), new("snare", 8), new("rest", 8)]),
            new ParentOutcome("vocals", ParentOutcomeKind.NotReliable, []),
            new ParentOutcome("bass", ParentOutcomeKind.NotSupported, []),
            new ParentOutcome("guitar", ParentOutcomeKind.NothingFurther, []),
            new ParentOutcome("piano", ParentOutcomeKind.SkippedSilent, []),
            new ParentOutcome("other", ParentOutcomeKind.Split, [new("strings", 8), new("brass", 8), new("rest", 8)]),
        ],
            SyncTestData.T0);
        return track;
    }

    private static CreateScoreRequest WithPart(Guid trackId, string channel, ScoreProfileDto? profile, ScorePartDto part) =>
        Request(trackId, channel) with { Profile = profile, Part = part };

    private async Task<ScoreOptionsDto> OptionsAsync(Guid trackId, string channel)
    {
        using var response = await Client.GetAsync(new Uri(ApiRoutes.ScoreOptionsForChannel(trackId, channel), UriKind.Relative), Ct);
        await ContractAssert.MatchesAsync(response, "getScoreOptions");
        return await response.BodyAsync<ScoreOptionsDto>();
    }

    [Theory]
    [MemberData(nameof(PartOptionCases))]
    public async Task Options_OfferPartsForEveryPitchedProfile_AndAClefOutsideTheGrandStaff(string channel, ScoreProfileDto suggested, (bool Available, bool ClefAllowed) expected)
    {
        var track = await Factory.SeedAsync(SectionTrack());

        var options = await OptionsAsync(track.Id, channel);

        options.SuggestedProfile.ShouldBe(suggested);
        (options.Parts!.Available, options.Parts.ClefAllowed, options.Parts.DefaultInstrument).ShouldBe((expected.Available, expected.ClefAllowed, WrittenInstrumentDto.Concert));
    }

    [Fact]
    public async Task ASectionStem_OffersItsProfileWithTheOthers_WithinTheContractLimits()
    {
        var track = await Factory.SeedAsync(SectionTrack());

        var options = await OptionsAsync(track.Id, "other.strings");

        options.AllowedProfiles.ShouldBe([ScoreProfileDto.Vocals, ScoreProfileDto.Bass, ScoreProfileDto.Guitar, ScoreProfileDto.Keys, ScoreProfileDto.Piano, ScoreProfileDto.Strings]);
        options.Limits.Select(limit => limit.Profile).ShouldBe(options.AllowedProfiles);
        options.Estimates.Count.ShouldBe(7);
    }

    [Fact]
    public async Task APart_IsSentToTheWorker_AndKeptInTheRecipe()
    {
        var track = await Factory.SeedAsync(SectionTrack());

        using var response = await Client.PostScoreAsync(WithPart(track.Id, "other.brass", null, new ScorePartDto { Instrument = WrittenInstrumentDto.HornF, Clef = ClefChoiceDto.Treble }));

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        await ContractAssert.MatchesAsync(response, "createScore");
        var sent = Worker.TranscriptionCreates.Single();
        (sent.Profile, sent.Part).ShouldBe((ScoreProfile.Brass, new WorkerPartSettings("horn-f", "treble")));
        var recipe = (await response.BodyAsync<AssetDto>()).Provenance.ScoreRecipe!;
        (recipe.Profile, recipe.Part!.Instrument, recipe.Part.Clef).ShouldBe((ScoreProfileDto.Brass, WrittenInstrumentDto.HornF, ClefChoiceDto.Treble));
    }

    [Theory]
    [InlineData("drums", ScoreProfileDto.Drums, ClefChoiceDto.Auto, "part-not-allowed")]
    [InlineData("piano", ScoreProfileDto.Piano, ClefChoiceDto.Treble, "clef-not-allowed")]
    public async Task ARefusedPartOrClef_Is422WithATypedProblem(string channel, ScoreProfileDto profile, ClefChoiceDto clef, string type)
    {
        var track = await Factory.SeedAsync(SectionTrack());

        using var response = await Client.PostScoreAsync(WithPart(track.Id, channel, profile, new ScorePartDto { Instrument = WrittenInstrumentDto.Concert, Clef = clef }));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        await ContractAssert.MatchesAsync(response, "createScore");
        (await response.JsonAsync()).GetProperty("type").GetString().ShouldBe(type);
        Worker.TranscriptionCreates.ShouldBeEmpty();
    }

    [Fact]
    public async Task TheFacts_ReportThePartSummary()
    {
        var track = await Factory.SeedAsync(SectionTrack());
        using var created = await Client.PostScoreAsync(WithPart(track.Id, "other.brass", null, new ScorePartDto { Instrument = WrittenInstrumentDto.HornF }));
        var asset = await created.BodyAsync<AssetDto>();
        var part = new WorkerPartSummary { Instrument = "horn-f", Name = "Horn in F", Interval = "P5", WrittenKey = "G", Clef = "treble", OutOfRange = 2 };
        CompleteTranscriptions(Worker, Result(Summary(ScoreProfile.Brass) with { Part = part }));

        await Factory.SyncAssetsAsync();

        var ready = await Client.GetFromJsonAsync<AssetDto>(ApiRoutes.Asset(asset.Id), ApiFactory.Json, Ct);
        var facts = ready!.Score!;
        (facts.Profile, facts.Part!.Instrument, facts.Part.Name, facts.Part.WrittenKey, facts.Part.OutOfRange).ShouldBe((ScoreProfileDto.Brass, WrittenInstrumentDto.HornF, "Horn in F", "G", 2));
    }
}
