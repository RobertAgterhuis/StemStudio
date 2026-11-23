using StemStudio.Domain.Assets;
using StemStudio.Domain.Handouts;

namespace StemStudio.Domain.Tests.Assets;

/// <summary>Feature 020 T020: handout library items (data-model 5.4).</summary>
public sealed class HandoutAssetTests
{
    private const string JobId = "1e2f3a4b5c6d4e7f8a9b0c1d2e3f4a5b";
    private static readonly DateTime T0 = new(2026, 11, 23, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void CreateHandout_QueuesAnItemWithItsPlan_WithoutPages_KeepingThePlannedWarnings()
    {
        var plan = Plan() with { Pages = 9, Outline = ["stale"], Warnings = [new HandoutWarning(HandoutWarningCode.NotationOmitted, "planned")] };

        var asset = Asset.CreateHandout(Guid.NewGuid(), "Example Song - Bass handout", new HandoutStart(Provenance(), plan), T0);

        (asset.Kind, asset.Status, asset.TrackId, asset.Provenance.Origin).ShouldBe((AssetKind.Handout, AssetStatus.Queued, asset.Provenance.TrackId, AssetOrigin.Handout));
        (asset.Handout!.Pages, asset.Handout.Outline.Count, asset.Handout.Warnings.Count, asset.Handout.Songs).ShouldBe((0, 0, 1, 1));
        asset.Handout.Omitted.ShouldBe([new OmittedIngredient(IngredientCode.Lyrics, OmissionReason.NotSelected)]);
    }

    [Fact]
    public void CreateHandout_NeedsTheHandoutOriginAndRecipe()
    {
        Should.Throw<ArgumentException>(() => Asset.CreateHandout(Guid.NewGuid(), "x", new HandoutStart(Provenance() with { HandoutRecipe = null }, Plan()), T0));
        Should.Throw<ArgumentException>(() => Asset.CreateHandout(Guid.NewGuid(), "x", new HandoutStart(Provenance() with { Origin = AssetOrigin.Export }, Plan()), T0));
        Should.Throw<ArgumentNullException>(() => Asset.CreateHandout(Guid.NewGuid(), "x", new HandoutStart(Provenance(), null!), T0));
        Should.Throw<ArgumentNullException>(() => Asset.CreateHandout(Guid.NewGuid(), "x", null!, T0));
    }

    [Fact]
    public void CompleteHandout_StoresThePdfPagesOutlineAndWarnings()
    {
        var asset = Queued();
        asset.AssignJob(JobId, T0);
        var file = new AssetFile("pdf", Asset.HandoutFileName, "application/pdf", 1200, new string('b', 64));

        asset.CompleteHandout([file], new HandoutRendering(5, ["Example Song", "Form"], [new HandoutWarning(HandoutWarningCode.NotationScaled, "scaled")]), T0.AddSeconds(4));

        (asset.Status, asset.SizeBytes, asset.Progress, asset.WorkerJobId).ShouldBe((AssetStatus.Ready, 1200L, 100, (string?)null));
        (asset.Handout!.Pages, asset.Handout.Outline.Count, asset.Handout.Warnings.Single().Code).ShouldBe((5, 2, HandoutWarningCode.NotationScaled));
        asset.Handout.Included.ShouldBe([IngredientCode.Facts]);
    }

    [Fact]
    public void CompleteHandout_PutsThePlannedWarningsFirst_AndKeepsAtMostFifty()
    {
        var planned = new HandoutWarning(HandoutWarningCode.NotationOmitted, "planned");
        var asset = Asset.CreateHandout(Guid.NewGuid(), "x", new HandoutStart(Provenance(), Plan() with { Warnings = [planned] }), T0);
        asset.AssignJob(JobId, T0);
        var file = new AssetFile("pdf", Asset.HandoutFileName, "application/pdf", 1200, new string('b', 64));
        var rendered = Enumerable.Range(0, Asset.MaxHandoutWarnings).Select(index => new HandoutWarning(HandoutWarningCode.NotationScaled, $"w{index}")).ToList();

        asset.CompleteHandout([file], new HandoutRendering(1, [], rendered), T0);

        asset.Handout!.Warnings.Count.ShouldBe(Asset.MaxHandoutWarnings);
        (asset.Handout.Warnings[0], asset.Handout.Warnings[1].Text).ShouldBe((planned, "w0"));
    }

    [Fact]
    public void Replan_TakesTheNewPlannedWarnings()
    {
        var asset = Asset.CreateHandout(Guid.NewGuid(), "x", new HandoutStart(Provenance(), Plan() with { Warnings = [new HandoutWarning(HandoutWarningCode.NotationOmitted, "old")] }), T0);

        asset.Replan(Plan() with { Pages = 3, Warnings = [] });

        (asset.Handout!.Pages, asset.Handout.Warnings.Count).ShouldBe((0, 0));
    }

    [Fact]
    public void CompleteHandout_RefusesOtherKindsAndNoPages()
    {
        var audio = Asset.CreateRecording(Guid.NewGuid(), "Take", new Provenance { Origin = AssetOrigin.Recording, TrackTitle = "Song", Contents = "Take", Recording = new RecordingSource(Guid.NewGuid()) }, T0);
        var file = new AssetFile("pdf", Asset.HandoutFileName, "application/pdf", 1, new string('b', 64));

        Should.Throw<InvalidOperationException>(() => audio.CompleteHandout([file], new HandoutRendering(1, [], []), T0));
        Should.Throw<ArgumentOutOfRangeException>(() => Queued().CompleteHandout([file], new HandoutRendering(0, [], []), T0));
        Should.Throw<ArgumentNullException>(() => Queued().CompleteHandout([file], new HandoutRendering(1, null!, []), T0));
        Should.Throw<ArgumentNullException>(() => Queued().CompleteHandout([file], new HandoutRendering(1, [], null!), T0));
        Should.Throw<InvalidOperationException>(() => audio.Replan(Plan()));
    }

    [Fact]
    public void Replan_ReplacesThePlanOfAFailedHandout()
    {
        var asset = Queued();
        asset.Fail("The handout could not be written", T0);
        asset.Retry(T0);

        asset.Replan(Plan() with { Songs = 3, Kind = HandoutKind.Setlist });

        (asset.Handout!.Songs, asset.Handout.Kind, asset.Status).ShouldBe((3, HandoutKind.Setlist, AssetStatus.Queued));
        Should.Throw<ArgumentNullException>(() => asset.Replan(null!));
    }

    private static Asset Queued() => Asset.CreateHandout(Guid.NewGuid(), "Example Song - Bass handout", new HandoutStart(Provenance(), Plan()), T0);

    private static HandoutFacts Plan() => new()
    {
        Kind = HandoutKind.Song,
        Family = HandoutFamily.Bass,
        Paper = Paper.A4,
        Songs = 1,
        Included = [IngredientCode.Facts],
        Omitted = [new OmittedIngredient(IngredientCode.Lyrics, OmissionReason.NotSelected)],
    };

    private static Provenance Provenance()
    {
        var trackId = Guid.NewGuid();
        return new Provenance
        {
            Origin = AssetOrigin.Handout,
            TrackId = trackId,
            TrackTitle = "Example Song",
            Contents = "Bass handout",
            HandoutRecipe = new HandoutRecipe { Kind = HandoutKind.Song, TrackId = trackId, Family = HandoutFamily.Bass, Paper = Paper.A4 },
        };
    }
}
