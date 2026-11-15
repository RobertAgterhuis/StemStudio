using System.Net;
using Bunit;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Scores;
using StemStudio.Web.Help;
using StemStudio.Web.Scores;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Scores;

/// <summary>The sheet music dialog (008 T043, FR-002, FR-003, FR-010, FR-014; ui-test-ids 1.7.0).</summary>
public sealed class ScoreDialogTests : ScoreTestContext
{
    private static readonly HelpCatalog Catalog = HelpCatalog.FromAssembly(typeof(HelpCatalog).Assembly);

    [Fact]
    public void Dialog_IsAModal_ForAChannel_WithItsHelpMarker()
    {
        var page = RenderDialog(ScoreData.Options());

        var dialog = page.Find("[data-testid=score-dialog]");
        (dialog.GetAttribute("role"), dialog.GetAttribute("aria-modal"), dialog.GetAttribute("data-source")).ShouldBe(("dialog", "true", "channel"));
        dialog.GetAttribute("data-help-id").ShouldBe("sheet-music");
        dialog.TextContent.ShouldContain("bass of Never Gonna Give You Up");
    }

    [Fact]
    public void Profile_PreselectsTheSuggestion_AndListsTheAllowedProfiles()
    {
        var page = RenderDialog(ScoreData.Options());

        var select = page.Find("[data-testid=score-profile]");
        select.GetAttribute("data-help-id").ShouldBe("sheet-music-profiles");
        select.QuerySelectorAll("option").Select(o => (o.GetAttribute("value"), o.TextContent)).ShouldBe(
            [("vocals", "Vocals (melody)"), ("bass", "Bass"), ("guitar", "Guitar"), ("keys", "Keys"), ("piano", "Piano")]);
        select.GetAttribute("value").ShouldBe("bass");
        page.Find("[data-testid=score-limits]").TextContent.ShouldBe("One note at a time; written an octave higher (bass clef 8vb); slides and ghost notes are missed.");
        page.Find("[data-testid=score-estimate]").TextContent.ShouldBe("about 31 seconds");
    }

    [Fact]
    public async Task ChangingTheProfile_UpdatesTheLimitsAndTheEstimate()
    {
        var page = RenderDialog(ScoreData.Options());

        await page.Find("[data-testid=score-profile]").ChangeAsync(new() { Value = "piano" });

        page.Find("[data-testid=score-limits]").TextContent.ShouldBe("Two staves split at middle C; the accurate model is slow on the CPU.");
        page.Find("[data-testid=score-estimate]").TextContent.ShouldBe("about 31 seconds");
    }

    [Fact]
    public async Task Submit_SendsTheDefaults_AndConfirmsWithALinkToTheItem()
    {
        var page = RenderDialog(ScoreData.Options());

        await page.Find("[data-testid=score-submit]").ClickAsync(new());

        var request = SentRequest();
        request.Source.ShouldBe(AudioSourceDto.FromChannel(TestData.TrackId, "bass"));
        (request.Profile, request.Quality, request.Resolution, request.KeyOverride, request.Tempo, request.Range)
            .ShouldBe((ScoreProfileDto.Bass, null, ScoreResolutionDto.Sixteenth, null, null, null));
        var saved = page.Find("[data-testid=score-saved]");
        saved.GetAttribute("role").ShouldBe("status");
        saved.TextContent.ShouldContain("Never Gonna Give You Up - bass (sheet music)");
        page.Find("[data-testid=score-saved-open]").GetAttribute("href").ShouldBe($"library?asset={ScoreData.ScoreId}");
        page.FindAll("[data-testid=score-submit]").ShouldBeEmpty();
    }

    [Fact]
    public async Task Submit_RaisesOnSaved_WithTheNewItem()
    {
        AssetDto? saved = null;
        Api.GetScoreOptionsAsync(Arg.Any<AudioSourceDto>(), Arg.Any<CancellationToken>()).Returns(ScoreData.Options());
        var page = Render<StemStudio.Web.Components.Scores.ScoreDialog>(p => p
            .Add(c => c.Target, ScoreTarget.ForChannel(TestData.Track(), "bass"))
            .Add(c => c.OnSaved, a => saved = a));

        await page.Find("[data-testid=score-submit]").ClickAsync(new());

        saved.ShouldNotBeNull().Id.ShouldBe(ScoreData.ScoreId);
    }

    [Fact]
    public async Task SubmitRefusal_IsShown_AndTheFormStays()
    {
        Api.CreateScoreAsync(Arg.Any<CreateScoreRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(Problem(HttpStatusCode.Conflict, "Not enough disk space", "Less than 1 GiB is free on the data drive; delete items or free space first."));
        var page = RenderDialog(ScoreData.Options());

        await page.Find("[data-testid=score-submit]").ClickAsync(new());

        page.Find("[data-testid=score-error]").TextContent.ShouldBe("Less than 1 GiB is free on the data drive; delete items or free space first.");
        page.Find("[data-testid=score-submit]").ShouldNotBeNull();
    }

    [Fact]
    public void OptionsFailure_IsShown()
    {
        Api.GetScoreOptionsAsync(Arg.Any<AudioSourceDto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(Problem(HttpStatusCode.Conflict, "Track not ready", "The track is still being processed; create sheet music when it is completed."));

        var page = Render<StemStudio.Web.Components.Scores.ScoreDialog>(p => p.Add(c => c.Target, ScoreTarget.ForChannel(TestData.Track(), "bass")));

        page.Find("[data-testid=score-error]").TextContent.ShouldContain("still being processed");
        page.FindAll("[data-testid=score-submit]").ShouldBeEmpty();
        page.Find("[data-testid=score-cancel]").ShouldNotBeNull();
    }

    [Fact]
    public async Task WithoutASuggestion_AProfileMustBeChosen()
    {
        var page = RenderDialog(ScoreData.Options(suggested: null));
        page.Find("[data-testid=score-profile]").QuerySelector("option")!.TextContent.ShouldBe("Choose an instrument");
        page.FindAll("[data-testid=score-limits]").ShouldBeEmpty();

        await page.Find("[data-testid=score-submit]").ClickAsync(new());

        page.Find("[data-testid=score-error]").TextContent.ShouldBe("Choose the instrument to notate.");
        await Api.DidNotReceiveWithAnyArgs().CreateScoreAsync(default!, Xunit.TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CancelAndEscape_Close()
    {
        var page = RenderDialog(ScoreData.Options());
        await page.Find("[data-testid=score-cancel]").ClickAsync(new());
        Closed.ShouldBeTrue();

        var again = RenderDialog(ScoreData.Options());
        await again.Find("[data-testid=score-dialog]").KeyDownAsync(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Escape" });
        Closed.ShouldBeTrue();
    }

    [Theory]
    [InlineData("sheet-music", null)]
    [InlineData("sheet-music-profiles", null)]
    public void Topics_ExplainTheirMarkers(string id, string? feature)
    {
        var topic = Catalog.Topic(id).ShouldNotBeNull();

        Catalog.TopicForAnchor(id)!.Id.ShouldBe(id);
        (topic.Area, topic.Feature, topic.Reviewed).ShouldBe(("tracks", feature, "008"));
    }
}
