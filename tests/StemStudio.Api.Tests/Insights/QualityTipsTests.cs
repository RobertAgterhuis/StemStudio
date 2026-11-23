using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.Insights;
using StemStudio.Domain.Insights;
using StemStudio.Domain.Tracks.Detail;

namespace StemStudio.Api.Tests.Insights;

/// <summary>
/// Feature 020 T051: the report view builds the tips from the current track state (detail phase and outcomes, stem set); a
/// detail split changes the tips without a new analysis (FR-005). No tip mentions the GPU (FR-006).
/// </summary>
public sealed class QualityTipsTests : ApiTestBase
{
    private ApiTestFactoryPair Pair => new(Factory, Client);

    [Fact]
    public async Task TheReport_CarriesTheTipsOfEveryStem_AndMatchesTheContract()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());
        await Pair.MeasureAsync(track.Id, QualityTestData.Result("fake-1", ("vocals", QualityRating.Fair), ("drums", QualityRating.Good), ("bass", QualityRating.Good), ("guitar", QualityRating.Poor), ("piano", QualityRating.NotPresent), ("other", QualityRating.Fair)));

        using var response = await Client.GetAsync(new Uri(ApiRoutes.Quality(track.Id), UriKind.Relative), Ct);

        await ContractAssert.MatchesAsync(response, "getQualityReport");
        var stems = (await response.BodyAsync<QualityReportDto>()).Stems.ToDictionary(stem => stem.Channel);
        stems["vocals"].Tips.Single().Action.ShouldBe(new TipActionDto { Kind = TipActionKindDto.SplitDetail, Channel = "vocals", Parent = "vocals", Child = null });
        stems["drums"].Tips.Single().Code.ShouldBe(TipCodeDto.Clean);
        stems["guitar"].Tips.Single().Code.ShouldBe(TipCodeDto.TryDetailSeparation);
        stems["piano"].Tips.Single().Action!.Kind.ShouldBe(TipActionKindDto.Mute);
        stems.Values.SelectMany(stem => stem.Tips).ShouldAllBe(tip => !tip.Text.Contains("GPU", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ADetailSplit_ChangesTheTips_WithoutANewAnalysis()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());
        await Pair.MeasureAsync(track.Id, QualityTestData.Result("fake-1", ("other", QualityRating.Fair), ("drums", QualityRating.Fair)));
        var before = (await Client.GetQualityAsync(track.Id)).Stems.ToDictionary(stem => stem.Channel);

        await Factory.UpdateTrackAsync(track.Id, changed =>
        {
            changed.RequestDetail(SyncTestData.T0);
            changed.CompleteDetail(
            [
                new ParentOutcome("vocals", ParentOutcomeKind.NotReliable, []),
                new ParentOutcome("drums", ParentOutcomeKind.Split, [new("kick", 8), new("snare", 8), new("rest", 8)]),
                new ParentOutcome("bass", ParentOutcomeKind.NotSupported, []),
                new ParentOutcome("guitar", ParentOutcomeKind.NothingFurther, []),
                new ParentOutcome("piano", ParentOutcomeKind.SkippedSilent, []),
                new ParentOutcome("other", ParentOutcomeKind.NothingFurther, []),
            ],
                SyncTestData.T0);
        });
        var after = (await Client.GetQualityAsync(track.Id)).Stems.ToDictionary(stem => stem.Channel);

        (before["other"].Tips.Single().Code, before["drums"].Tips.Single().Code).ShouldBe((TipCodeDto.TryDetailSeparation, TipCodeDto.TryDetailSeparation));
        after["other"].Tips.Single().Code.ShouldBe(TipCodeDto.HardToIsolate);
        after["drums"].Tips.ShouldBeEmpty("the split exists, its children are not measured yet");
        Factory.InsightsWorker.QualityRequests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task AMeasuredBetterChild_IsSuggested()
    {
        var track = await Factory.SeedAsync(SyncTestData.DetailCompleted());
        await Pair.MeasureAsync(track.Id, QualityTestData.Result("fake-1", ("drums", QualityRating.Poor), ("drums.kick", QualityRating.Good), ("drums.snare", QualityRating.Fair), ("drums.rest", QualityRating.Good)));

        var drums = (await Client.GetQualityAsync(track.Id)).Stems.Single(stem => stem.Channel == "drums");

        drums.Tips.Single().ShouldBe(new TipDto
        {
            Code = TipCodeDto.UseDetailStem,
            Text = "Use the kick detail stem instead.",
            Action = new TipActionDto { Kind = TipActionKindDto.OpenDetail, Channel = "drums", Parent = "drums", Child = "kick" },
        });
    }
}
