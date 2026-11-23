using Bunit;
using StemStudio.Contracts.Scores;
using StemStudio.Web.Experience;
using StemStudio.Web.Scores;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Scores;

/// <summary>"Include chords" of the sheet music dialog (016 T074, FR-017): Advanced, only for vocals/guitar/keys/piano, enabled when the chords are ready.</summary>
public sealed class ScoreChordsTests : ScoreTestContext
{
    private static readonly ScoreTarget Guitar = ScoreTarget.ForChannel(TestData.Track(), "guitar");

    private static ScoreOptionsDto Options(bool available, ScoreProfileDto profile = ScoreProfileDto.Guitar) =>
        ScoreData.Options("guitar", profile) with { Chords = new ScoreChordOptionsDto { Available = available } };

    [Fact]
    public async Task Available_IsEnabled_AndSent()
    {
        var page = RenderDialogAt(ExperienceLevel.Advanced, Options(true), Guitar);
        var box = page.Find("[data-testid=score-include-chords]");
        (box.HasAttribute("disabled"), box.HasAttribute("aria-describedby")).ShouldBe((false, false));

        await box.ChangeAsync(new() { Value = true });
        await page.Find("[data-testid=score-submit]").ClickAsync(new());

        SentRequest().IncludeChords.ShouldBeTrue();
    }

    [Fact]
    public async Task NotReady_IsDisabled_WithTheReason_AndNeverSent()
    {
        var page = RenderDialogAt(ExperienceLevel.Expert, Options(false), Guitar);
        var box = page.Find("[data-testid=score-include-chords]");

        box.HasAttribute("disabled").ShouldBeTrue();
        page.Find($"#{box.GetAttribute("aria-describedby")}").TextContent.ShouldContain("not ready");
        await page.Find("[data-testid=score-submit]").ClickAsync(new());
        SentRequest().IncludeChords.ShouldBeFalse();
    }

    [Fact]
    public void Beginner_DoesNotSeeIt() =>
        RenderDialogAt(ExperienceLevel.Beginner, Options(true), Guitar).FindAll("[data-testid=score-include-chords]").ShouldBeEmpty();

    [Theory]
    [InlineData(ScoreProfileDto.Bass)]
    [InlineData(ScoreProfileDto.Drums)]
    public void NotForBassOrDrums(ScoreProfileDto profile) =>
        RenderDialogAt(ExperienceLevel.Advanced, Options(true, profile), Guitar).FindAll("[data-testid=score-include-chords]").ShouldBeEmpty();

    [Fact]
    public void OlderBackend_WithoutChordOptions_ShowsItDisabled() =>
        RenderDialogAt(ExperienceLevel.Advanced, ScoreData.Options("guitar", ScoreProfileDto.Guitar), Guitar)
            .Find("[data-testid=score-include-chords]").HasAttribute("disabled").ShouldBeTrue();
}
