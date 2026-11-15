using Bunit;
using StemStudio.Contracts;
using StemStudio.Contracts.Scores;
using StemStudio.Web.Experience;
using StemStudio.Web.Help;
using StemStudio.Web.Scores;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Scores;

/// <summary>Key, rhythm and tempo of the sheet music dialog (008 T048, FR-005..FR-007, FR-014).</summary>
public sealed class ScoreKeyTempoTests : ScoreTestContext
{
    private static readonly HelpCatalog Catalog = HelpCatalog.FromAssembly(typeof(HelpCatalog).Assembly);

    [Fact]
    public async Task SimplifiedRhythm_IsOffered_AndSendsEighthNotes()
    {
        var page = RenderDialogAt(ExperienceLevel.Beginner, ScoreData.Options());
        var box = page.Find("[data-testid=score-resolution]");
        (box.GetAttribute("type"), box.HasAttribute("checked")).ShouldBe(("checkbox", false));

        await box.ChangeAsync(new() { Value = true });
        await page.Find("[data-testid=score-submit]").ClickAsync(new());

        SentRequest().Resolution.ShouldBe(ScoreResolutionDto.Eighth);
    }

    [Fact]
    public async Task Vocals_AreSimplifiedByDefault()
    {
        var page = RenderDialog(ScoreData.Options());

        await page.Find("[data-testid=score-profile]").ChangeAsync(new() { Value = "vocals" });

        page.Find("[data-testid=score-resolution]").HasAttribute("checked").ShouldBeTrue();
    }

    [Fact]
    public async Task KeyOverride_ListsAutomaticAndTheThirtyKeys_AndIsSent()
    {
        var page = RenderDialog(ScoreData.Options());
        var select = page.Find("[data-testid=score-key-override]");
        select.GetAttribute("data-help-id").ShouldBe("sheet-music-key-tempo");
        var options = select.QuerySelectorAll("option").Select(o => (o.GetAttribute("value"), o.TextContent)).ToList();
        options.Count.ShouldBe(31);
        options[0].ShouldBe(("", "Automatic (estimated from the notes)"));
        options.ShouldContain(("E minor", "E minor"));
        options.ShouldContain(("B- major", "B-flat major"));
        options.ShouldContain(("F# minor", "F-sharp minor"));

        await select.ChangeAsync(new() { Value = "E minor" });
        await page.Find("[data-testid=score-submit]").ClickAsync(new());

        SentRequest().KeyOverride.ShouldBe("E minor");
    }

    [Fact]
    public void KeyOverride_IsAdvanced_AndNotForDrums()
    {
        RenderDialogAt(ExperienceLevel.Beginner, ScoreData.Options()).FindAll("[data-testid=score-key-override]").ShouldBeEmpty();
        RenderDialog(ScoreData.DrumOptions(requireSplit: false), ScoreTarget.ForChannel(TestData.Track(), "drums"))
            .FindAll("[data-testid=score-key-override]").ShouldBeEmpty();
    }

    [Fact]
    public async Task UnreliableGrid_RequiresATempo_PrefilledFromTheTrack_EvenAtBeginner()
    {
        var page = RenderDialogAt(ExperienceLevel.Beginner, ScoreData.Options(grid: GridStatusDto.Unreliable));

        var notice = page.Find("[data-testid=score-tempo-required]");
        notice.GetAttribute("data-help-id").ShouldBe("sheet-music-key-tempo");
        notice.TextContent.ShouldContain("enter the tempo");
        page.Find("[data-testid=score-tempo-bpm]").GetAttribute("value").ShouldBe("113.5");
        page.Find("[data-testid=score-tempo-beats-per-bar]").GetAttribute("value").ShouldBe("4");
        page.Find("[data-testid=score-tempo-first-downbeat]").GetAttribute("value").ShouldBe("0.52");
        await page.Find("[data-testid=score-submit]").ClickAsync(new());

        SentRequest().Tempo.ShouldBe(new ScoreTempoDto(113.5, 4, 0.52));
    }

    [Theory]
    [InlineData("", "4", "0", "Enter the tempo, the beats per bar and the first downbeat.")]
    [InlineData("20", "4", "0", "The tempo must be between 30 and 300 BPM.")]
    [InlineData("120", "13", "0", "The beats per bar must be a whole number from 1 to 12.")]
    [InlineData("120", "3.5", "0", "The beats per bar must be a whole number from 1 to 12.")]
    [InlineData("120", "4", "-1", "The first downbeat must be between 0 and 900 seconds.")]
    public async Task RequiredTempo_IsChecked(string bpm, string beats, string downbeat, string message)
    {
        var target = ScoreTarget.ForChannel(TestData.Track() with { BeatGrid = null }, "bass");
        var page = RenderDialog(ScoreData.Options(grid: GridStatusDto.None), target);
        page.Find("[data-testid=score-tempo-bpm]").GetAttribute("value").ShouldBeNullOrEmpty();

        await page.Find("[data-testid=score-tempo-bpm]").InputAsync(new() { Value = bpm });
        await page.Find("[data-testid=score-tempo-beats-per-bar]").InputAsync(new() { Value = beats });
        await page.Find("[data-testid=score-tempo-first-downbeat]").InputAsync(new() { Value = downbeat });
        await page.Find("[data-testid=score-submit]").ClickAsync(new());

        page.Find("[data-testid=score-error]").TextContent.ShouldBe(message);
    }

    [Fact]
    public async Task ReliableGrid_HasOptionalTempoFields_AtAdvanced()
    {
        var page = RenderDialog(ScoreData.Options());
        page.FindAll("[data-testid=score-tempo-required]").ShouldBeEmpty();
        page.Find("[data-testid=score-tempo-bpm]").GetAttribute("value").ShouldBeNullOrEmpty();

        await page.Find("[data-testid=score-submit]").ClickAsync(new());

        SentRequest().Tempo.ShouldBeNull();
    }

    [Fact]
    public async Task ReliableGrid_ManualTempo_IsSentWhenComplete()
    {
        var page = RenderDialog(ScoreData.Options());

        await page.Find("[data-testid=score-tempo-bpm]").InputAsync(new() { Value = "96" });
        await page.Find("[data-testid=score-submit]").ClickAsync(new());
        page.Find("[data-testid=score-error]").TextContent.ShouldBe("Enter the tempo, the beats per bar and the first downbeat, or leave all three empty.");
        await page.Find("[data-testid=score-tempo-beats-per-bar]").InputAsync(new() { Value = "3" });
        await page.Find("[data-testid=score-tempo-first-downbeat]").InputAsync(new() { Value = "0.4" });
        await page.Find("[data-testid=score-submit]").ClickAsync(new());

        SentRequest().Tempo.ShouldBe(new ScoreTempoDto(96, 3, 0.4));
    }

    [Fact]
    public void ReliableGrid_HidesTheTempoAtBeginner() =>
        RenderDialogAt(ExperienceLevel.Beginner, ScoreData.Options()).FindAll("[data-testid=score-tempo-bpm]").ShouldBeEmpty();

    [Fact]
    public void KeyNames_AreReadable() =>
        ContractLimits.KeyNames.Select(ScoreText.KeyName).ShouldAllBe(name => !name.Contains("- ") && !name.Contains("# "));

    [Fact]
    public void KeyTempoTopic_ExplainsItsMarkers()
    {
        var topic = Catalog.Topic("sheet-music-key-tempo").ShouldNotBeNull();

        Catalog.TopicForAnchor("sheet-music-key-tempo")!.Id.ShouldBe("sheet-music-key-tempo");
        (topic.Feature, topic.Reviewed).ShouldBe(("score.key-override", "008"));
        topic.Markdown.ShouldContain("first downbeat");
    }
}
