using Bunit;
using NSubstitute;
using StemStudio.Contracts.Handouts;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Components.Pages;
using StemStudio.Web.Experience;
using StemStudio.Web.Handouts;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Handouts;

/// <summary>
/// Handouts on the track page (T076-T079, Q3): the menu and the notes editor on a completed track, and the create links
/// of missing ingredients going to existing flows only.
/// </summary>
public sealed class TrackHandoutTests : PageTestContext
{
    private static readonly Guid Track = TestData.TrackId;

    public TrackHandoutTests()
    {
        Api.GetHandoutOptionsAsync(Arg.Any<HandoutSourceDto>(), Arg.Any<HandoutFamilyDto>(), Arg.Any<TranspositionDto?>(), Arg.Any<CancellationToken>())
            .Returns(InsightsData.BassOptions());
    }

    private IRenderedComponent<Microsoft.AspNetCore.Components.CascadingValue<ExperienceContext>> RenderPage(TrackDto? track = null)
    {
        Api.GetTrackAsync(Track, Arg.Any<CancellationToken>()).Returns(track ?? TestData.Track());
        return RenderAtLevel<TrackDetail>(ExperienceLevel.Advanced, p => p.Add(c => c.Id, Track));
    }

    [Fact]
    public void CompletedTrack_HasTheMenuAndTheNotes()
    {
        var page = RenderPage();

        page.Find("[data-testid=handout-menu]").ShouldNotBeNull();
        page.Find("[data-testid=practice-notes]").ShouldNotBeNull();
        var heading = page.Find("#handouts-heading");
        heading.TextContent.ShouldBe("Lesson handouts");
    }

    [Fact]
    public void UnfinishedTrack_HasNeither()
    {
        var page = RenderPage(TestData.Track(TrackStatusDto.Separating));

        page.FindAll("[data-testid=handout-menu], [data-testid=practice-notes]").ShouldBeEmpty();
    }

    [Fact]
    public async Task CreateTab_OpensTheScoreDialogForTheFamilysStem()
    {
        Api.GetScoreOptionsAsync(Arg.Any<StemStudio.Contracts.Assets.AudioSourceDto>(), Arg.Any<CancellationToken>()).Returns(ScoreData.Options());
        var page = RenderPage();
        page.Find("[data-testid=handout-menu]").Click();
        page.Find("[data-testid=handout-customise]").Click();
        page.Find("[data-testid=handout-family]").Change("bass");

        await page.Find("[data-testid=handout-ingredient-action-notation]").ClickAsync(new());

        page.FindAll("[data-testid=handout-dialog]").ShouldBeEmpty();
        page.Find("[data-testid=score-dialog]").TextContent.ShouldContain("bass");
    }

    [Fact]
    public async Task WriteNotes_SelectsTheFamilyInTheNotesEditor()
    {
        var page = RenderPage();
        page.Find("[data-testid=handout-menu]").Click();
        page.Find("[data-testid=handout-customise]").Click();
        page.Find("[data-testid=handout-family]").Change("bass");

        await page.Find("[data-testid=handout-ingredient-action-practice-notes]").ClickAsync(new());

        page.Find("[data-testid=practice-notes-family]").GetAttribute("value").ShouldBe("bass");
    }

    [Theory]
    [InlineData(IngredientActionDto.FindLyrics, HandoutFamilyDto.Vocals, "#lyrics-heading")]
    [InlineData(IngredientActionDto.DetectChords, HandoutFamilyDto.Guitar, "#lyrics-heading")]
    [InlineData(IngredientActionDto.SplitDrums, HandoutFamilyDto.Drums, "#detail-heading")]
    [InlineData(IngredientActionDto.DetectSections, HandoutFamilyDto.Overview, "/mixer")]
    public void OtherLinks_GoToTheExistingCards(IngredientActionDto action, HandoutFamilyDto family, string end)
    {
        IngredientLinks.Target(Track, action, family).Url!.ShouldEndWith(end);
    }

    [Theory]
    [InlineData(IngredientActionDto.CreateScore, HandoutFamilyDto.Keys, "piano")]
    [InlineData(IngredientActionDto.CreateDrumNotation, HandoutFamilyDto.Drums, "drums")]
    [InlineData(IngredientActionDto.CreateTransposedPart, HandoutFamilyDto.Melodic, "other")]
    [InlineData(IngredientActionDto.CreateTab, HandoutFamilyDto.Guitar, "guitar")]
    [InlineData(IngredientActionDto.CreateScore, HandoutFamilyDto.Vocals, "vocals")]
    [InlineData(IngredientActionDto.CreateScore, HandoutFamilyDto.Overview, "other")]
    public void ScoreLinks_OpenTheScoreDialogForTheFamilysStem(IngredientActionDto action, HandoutFamilyDto family, string stem)
    {
        var target = IngredientLinks.Target(Track, action, family);

        (target.ScoreStem, target.Url, target.Notes).ShouldBe((stem, (string?)null, false));
    }

    [Fact]
    public async Task LyricsLink_NavigatesToTheCard()
    {
        Api.GetHandoutOptionsAsync(Arg.Any<HandoutSourceDto>(), Arg.Any<HandoutFamilyDto>(), Arg.Any<TranspositionDto?>(), Arg.Any<CancellationToken>())
            .Returns(InsightsData.BassOptions() with
            {
                Ingredients = [new IngredientDto { Code = IngredientCodeDto.ChordsOverLyrics, Status = IngredientStatusDto.Missing, DefaultIncluded = true, Text = "No lyrics yet", Action = IngredientActionDto.FindLyrics }],
            });
        var page = RenderPage();
        page.Find("[data-testid=handout-menu]").Click();
        page.Find("[data-testid=handout-customise]").Click();

        await page.Find("[data-testid=handout-ingredient-action-chords-over-lyrics]").ClickAsync(new());

        NavigatedTo.ShouldEndWith($"tracks/{Track}#lyrics-heading");
    }
}
