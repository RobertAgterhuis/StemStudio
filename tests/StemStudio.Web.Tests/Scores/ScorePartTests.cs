using Bunit;
using NSubstitute;
using StemStudio.Contracts.InstrumentViews;
using StemStudio.Contracts.Scores;
using StemStudio.Web.Components.Library;
using StemStudio.Web.Experience;
using StemStudio.Web.Scores;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Scores;

/// <summary>Parts for transposing instruments in the sheet music dialog and the library (016 T110, FR-051, FR-052, FR-061).</summary>
public sealed class ScorePartTests : ScoreTestContext
{
    private static readonly ScoreTarget Brass = ScoreTarget.ForChannel(TestData.Track(), "other");

    private static ScoreOptionsDto Options(ScoreProfileDto profile = ScoreProfileDto.Brass, bool clefAllowed = true, bool available = true) =>
        ScoreData.Options("other", profile) with
        {
            AllowedProfiles = [ScoreProfileDto.Brass, ScoreProfileDto.Strings, ScoreProfileDto.Woodwinds, ScoreProfileDto.Keys, ScoreProfileDto.Drums],
            Parts = new ScorePartOptionsDto { Available = available, DefaultInstrument = WrittenInstrumentDto.Concert, ClefAllowed = clefAllowed },
        };

    private static string[] OptionValues(IRenderedComponent<Microsoft.AspNetCore.Components.CascadingValue<ExperienceContext>> page, string id) =>
        [.. page.FindAll($"[data-testid={id}] option").Select(o => o.GetAttribute("value")!)];

    [Fact]
    public async Task Beginner_PicksAnInstrument_ConcertFirst_WithoutAClef()
    {
        var page = RenderDialogAt(ExperienceLevel.Beginner, Options(), Brass);

        page.WaitForAssertion(() => OptionValues(page, "score-part-instrument").ShouldBe(["concert", "alto-sax-eb", "viola"]));
        page.Find("[data-testid=score-part-instrument] option[value=concert]").TextContent.ShouldBe("Concert pitch (no transposition)");
        page.Find("[data-testid=score-part-instrument] option[value=alto-sax-eb]").TextContent.ShouldBe("Alto Saxophone in E-flat");
        page.FindAll("[data-testid=score-part-clef]").ShouldBeEmpty();

        await page.Find("[data-testid=score-part-instrument]").ChangeAsync(new() { Value = "alto-sax-eb" });
        await page.Find("[data-testid=score-submit]").ClickAsync(new());

        SentRequest().Part.ShouldBe(new ScorePartDto { Instrument = WrittenInstrumentDto.AltoSaxEb, Clef = ClefChoiceDto.Auto });
        SentRequest().Profile.ShouldBe(ScoreProfileDto.Brass);
    }

    [Fact]
    public async Task ConcertAndAutomatic_SendNoPart()
    {
        var page = RenderDialogAt(ExperienceLevel.Advanced, Options(), Brass);
        page.WaitForAssertion(() => page.Find("[data-testid=score-part-instrument]"));

        await page.Find("[data-testid=score-submit]").ClickAsync(new());

        SentRequest().Part.ShouldBeNull();
    }

    [Fact]
    public async Task Advanced_ChoosesAClef()
    {
        var page = RenderDialogAt(ExperienceLevel.Advanced, Options(ScoreProfileDto.Strings), Brass);

        page.WaitForAssertion(() => OptionValues(page, "score-part-clef").ShouldBe(["auto", "treble", "bass", "alto", "tenor", "treble-8vb", "bass-8vb"]));
        page.Find("[data-testid=score-part-clef] option[value=treble-8vb]").TextContent.ShouldBe("Treble 8vb");
        await page.Find("[data-testid=score-part-instrument]").ChangeAsync(new() { Value = "viola" });
        await page.Find("[data-testid=score-part-clef]").ChangeAsync(new() { Value = "alto" });
        await page.Find("[data-testid=score-submit]").ClickAsync(new());

        SentRequest().Part.ShouldBe(new ScorePartDto { Instrument = WrittenInstrumentDto.Viola, Clef = ClefChoiceDto.Alto });
    }

    [Fact]
    public async Task GrandStaffProfiles_HaveNoClef_AndDrumsNoPart()
    {
        var page = RenderDialogAt(ExperienceLevel.Expert, Options(), Brass);
        page.WaitForAssertion(() => page.Find("[data-testid=score-part-clef]"));

        await page.Find("[data-testid=score-profile]").ChangeAsync(new() { Value = "keys" });
        page.FindAll("[data-testid=score-part-clef]").ShouldBeEmpty();
        page.FindAll("[data-testid=score-part-instrument]").Count.ShouldBe(1);

        await page.Find("[data-testid=score-profile]").ChangeAsync(new() { Value = "drums" });
        page.FindAll("[data-testid=score-part-instrument]").ShouldBeEmpty();
    }

    [Fact]
    public void ClefNotAllowed_OrPartsUnavailable_HideTheFields()
    {
        RenderDialogAt(ExperienceLevel.Expert, Options(clefAllowed: false), Brass).FindAll("[data-testid=score-part-clef]").ShouldBeEmpty();
        RenderDialogAt(ExperienceLevel.Expert, Options(available: false), Brass).FindAll("[data-testid=score-part-instrument]").ShouldBeEmpty();
        RenderDialogAt(ExperienceLevel.Expert, Options() with { Parts = null }, Brass).FindAll("[data-testid=score-part-instrument]").ShouldBeEmpty();
    }

    [Fact]
    public void NewProfiles_AreNamed()
    {
        var page = RenderDialogAt(ExperienceLevel.Beginner, Options(), Brass);

        page.Find("[data-testid=score-profile] option[value=strings]").TextContent.ShouldBe("Strings");
        page.Find("[data-testid=score-profile] option[value=woodwinds]").TextContent.ShouldBe("Woodwinds");
        ScoreText.Limit(ScoreProfileDto.Brass).ShouldContain("loudest line");
    }

    [Fact]
    public void Library_ShowsThePart_WithTheNotesOutOfRange()
    {
        var facts = ScoreData.Facts(ScoreProfileDto.Brass) with
        {
            Part = new PartFactsDto { Instrument = WrittenInstrumentDto.AltoSaxEb, Name = "Alto Saxophone in E-flat", Interval = "M6", WrittenKey = "A major", Clef = "treble", OutOfRange = 2 },
        };
        Api.ListAssetsAsync(Arg.Any<CancellationToken>()).Returns(AssetData.List(ScoreData.Asset(facts: facts)));

        var page = Render<AllItemsView>();

        page.WaitForAssertion(() => page.Find($"[data-testid=asset-score-part-{ScoreData.ScoreId}]").TextContent
            .ShouldBe("Alto Saxophone in E-flat, treble clef (2 notes out of range)"));
    }

    [Theory]
    [InlineData("treble-8vb", 1, "Viola, treble 8vb clef (1 note out of range)")]
    [InlineData("alto", 0, "Viola, alto clef")]
    public void PartText_SpellsOutTheClefAndTheRange(string clef, int outOfRange, string text) =>
        PartFactsText.Of(new PartFactsDto { Instrument = WrittenInstrumentDto.Viola, Name = "Viola", Interval = "P1", WrittenKey = null, Clef = clef, OutOfRange = outOfRange })
            .ShouldBe(text);
}
