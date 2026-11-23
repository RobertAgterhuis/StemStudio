using Bunit;
using NSubstitute;
using StemStudio.Contracts.InstrumentViews;
using StemStudio.Contracts.Scores;
using StemStudio.Web.Components.Library;
using StemStudio.Web.Scores;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Scores;

/// <summary>The tablature line of a ready score item in the library (T081, <c>asset-score-tab-&lt;id&gt;</c>).</summary>
public sealed class ScoreFactsTabTests : PageTestContext
{
    private static TablatureFactsDto Tab(int capo = 2, int moved = 3, int dropped = 0, int wide = 0) => new()
    {
        Tuning = TuningIdDto.GuitarDropD,
        Strings = 6,
        Capo = capo,
        Layout = TabLayoutDto.NotationAndTab,
        OctaveMoved = moved,
        NotesDropped = dropped,
        WideStretches = wide,
    };

    private string TextOf(TablatureFactsDto? tab)
    {
        var facts = ScoreData.Facts(ScoreProfileDto.Guitar) with { Tablature = tab };
        Api.ListAssetsAsync(Arg.Any<CancellationToken>()).Returns(AssetData.List(ScoreData.Asset(facts: facts)));
        var page = Render<AllItemsView>();
        page.WaitForAssertion(() => page.FindAll($"[data-testid=asset-score-tempo-{ScoreData.ScoreId}]").Count.ShouldBe(1));
        return page.FindAll($"[data-testid=asset-score-tab-{ScoreData.ScoreId}]").SingleOrDefault()?.TextContent ?? string.Empty;
    }

    [Fact]
    public void Tablature_ShowsTuningCapoAndTheMovedNotes()
    {
        TextOf(Tab()).ShouldBe("Tab: Drop D, capo 2 (3 notes moved by an octave)");
    }

    [Fact]
    public void EveryLimit_IsSpelledOut_AndNothingIsShownForZero()
    {
        TextOf(Tab(capo: 0, moved: 1, dropped: 2, wide: 4))
            .ShouldBe("Tab: Drop D (1 note moved by an octave, 2 chord notes left out, 4 chords with a wide stretch)");
        TextOf(Tab(capo: 0, moved: 0)).ShouldBe("Tab: Drop D");
    }

    [Fact]
    public void WithoutTablatureFacts_NoTabLineIsShown() => TextOf(null).ShouldBeEmpty();

    [Fact]
    public void SingleLimits_AreSingular()
    {
        TabFactsText.Of(Tab(capo: 0, moved: 0, dropped: 1, wide: 1) with { Tuning = TuningIdDto.Bass5Standard })
            .ShouldBe("Tab: 5-string (B E A D G) (1 chord note left out, 1 chord with a wide stretch)");
    }

    [Fact]
    public void EveryCatalogueTuning_HasALabel_AndAnUnknownOneFallsBackToItsName()
    {
        foreach (var tuning in Enum.GetValues<TuningIdDto>())
        {
            TabFactsText.Label(tuning).ShouldNotBe(tuning.ToString());
        }

        TabFactsText.Label((TuningIdDto)99).ShouldBe("99");
    }
}
