using Bunit;
using NSubstitute;
using StemStudio.Contracts.InstrumentViews;
using StemStudio.Contracts.Scores;
using StemStudio.Web.Api;
using StemStudio.Web.Experience;
using StemStudio.Web.Scores;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Scores;

/// <summary>Tablature options of the sheet music dialog (016 T081, FR-020, FR-078; ui-test-ids score-tab-*).</summary>
public sealed class ScoreTablatureTests : ScoreTestContext
{
    private static readonly ScoreTarget Guitar = ScoreTarget.ForChannel(TestData.Track(), "guitar");

    private static readonly TuningIdDto[] AllTunings =
    [
        TuningIdDto.GuitarStandard, TuningIdDto.GuitarDropD, TuningIdDto.GuitarDadgad,
        TuningIdDto.Bass4Standard, TuningIdDto.Bass4DropD, TuningIdDto.Bass5Standard,
    ];

    private static ScoreOptionsDto Options(ScoreProfileDto profile = ScoreProfileDto.Guitar, bool available = true) =>
        ScoreData.Options(profile == ScoreProfileDto.Bass ? "bass" : "guitar", profile) with
        {
            Tablature = new ScoreTablatureOptionsDto
            {
                Available = available,
                DefaultTuning = profile == ScoreProfileDto.Bass ? TuningIdDto.Bass4Standard : TuningIdDto.GuitarStandard,
                Tunings = AllTunings,
                CapoAllowed = profile == ScoreProfileDto.Guitar,
            },
        };

    private static string[] OptionValues(IRenderedComponent<Microsoft.AspNetCore.Components.CascadingValue<ExperienceContext>> page, string id) =>
        [.. page.FindAll($"[data-testid={id}] option").Select(o => o.GetAttribute("value")!)];

    [Fact]
    public async Task Beginner_Guitar_GetsLayoutAndStandardOrDropD_AndSendsNotationAndTab()
    {
        var page = RenderDialogAt(ExperienceLevel.Beginner, Options(), Guitar);

        OptionValues(page, "score-tab-layout").ShouldBe(["notation-and-tab", "tab-only", "none"]);
        OptionValues(page, "score-tab-tuning").ShouldBe(["guitar-standard", "guitar-drop-d"]);
        page.FindAll("[data-testid=score-tab-capo]").ShouldBeEmpty();
        page.Find("[data-testid=score-tab-tuning] option[value=guitar-standard]").TextContent.ShouldBe("Standard (E A D G B E)");

        await page.Find("[data-testid=score-tab-tuning]").ChangeAsync(new() { Value = "guitar-drop-d" });
        await page.Find("[data-testid=score-submit]").ClickAsync(new());

        SentRequest().Tablature.ShouldBe(new ScoreTablatureDto { Tuning = TuningIdDto.GuitarDropD, Capo = 0, Layout = TabLayoutDto.NotationAndTab });
    }

    [Fact]
    public async Task Advanced_Guitar_GetsEveryGuitarTuningAndTheCapo_PrefilledFromCapoMode()
    {
        var page = RenderDialogAt(ExperienceLevel.Advanced, Options(), Guitar with { Capo = 3 });

        OptionValues(page, "score-tab-tuning").ShouldBe(["guitar-standard", "guitar-drop-d", "guitar-dadgad"]);
        var capo = page.Find("[data-testid=score-tab-capo]");
        capo.GetAttribute("value").ShouldBe("3");

        await capo.ChangeAsync(new() { Value = "15" });
        await page.Find("[data-testid=score-tab-layout]").ChangeAsync(new() { Value = "tab-only" });
        await page.Find("[data-testid=score-submit]").ClickAsync(new());

        SentRequest().Tablature.ShouldBe(new ScoreTablatureDto { Tuning = TuningIdDto.GuitarStandard, Capo = 12, Layout = TabLayoutDto.TabOnly });
    }

    [Fact]
    public void TheOnlyAllowedProfile_SeedsTheTuning_WhenNothingIsSuggested()
    {
        var options = Options(ScoreProfileDto.Bass) with { SuggestedProfile = null, AllowedProfiles = [ScoreProfileDto.Bass] };
        options = options with { Tablature = options.Tablature! with { DefaultTuning = TuningIdDto.GuitarStandard } };

        var form = new ScoreForm(options, ScoreTarget.ForChannel(TestData.Track(), "bass"));

        form.Profile.ShouldBe(ScoreProfileDto.Bass);
        form.Request().Tablature!.Tuning.ShouldBe(TuningIdDto.Bass4Standard);
    }

    [Fact]
    public async Task WithoutATuningForTheProfile_NoTablatureIsOfferedOrSent()
    {
        var options = Options(ScoreProfileDto.Bass);
        options = options with { Tablature = options.Tablature! with { DefaultTuning = TuningIdDto.GuitarStandard, Tunings = [TuningIdDto.GuitarStandard, TuningIdDto.GuitarDropD] } };
        var page = RenderDialogAt(ExperienceLevel.Expert, options, ScoreTarget.ForChannel(TestData.Track(), "bass"));

        page.FindAll("[data-testid=score-tab-layout], [data-testid=score-tab-tuning]").ShouldBeEmpty();
        await page.Find("[data-testid=score-submit]").ClickAsync(new());

        SentRequest().Tablature.ShouldBeNull();
    }

    [Fact]
    public async Task Bass_HasNoCapo_AndOffersBassTuningsOnly()
    {
        var page = RenderDialogAt(ExperienceLevel.Expert, Options(ScoreProfileDto.Bass), ScoreTarget.ForChannel(TestData.Track(), "bass"));

        OptionValues(page, "score-tab-tuning").ShouldBe(["bass-4-standard", "bass-4-drop-d", "bass-5-standard"]);
        page.FindAll("[data-testid=score-tab-capo]").ShouldBeEmpty();

        await page.Find("[data-testid=score-tab-tuning]").ChangeAsync(new() { Value = "bass-5-standard" });
        await page.Find("[data-testid=score-submit]").ClickAsync(new());
        SentRequest().Tablature!.Tuning.ShouldBe(TuningIdDto.Bass5Standard);
    }

    [Fact]
    public async Task LayoutNone_SendsNoTablature_AndHidesTheTuning()
    {
        var page = RenderDialogAt(ExperienceLevel.Advanced, Options(), Guitar);

        await page.Find("[data-testid=score-tab-layout]").ChangeAsync(new() { Value = "none" });

        page.FindAll("[data-testid=score-tab-tuning]").ShouldBeEmpty();
        await page.Find("[data-testid=score-submit]").ClickAsync(new());
        SentRequest().Tablature.ShouldBeNull();
    }

    [Fact]
    public async Task SwitchingTheProfileToBass_TakesABassTuning_AndToVocals_HidesTheTab()
    {
        var page = RenderDialogAt(ExperienceLevel.Advanced, Options(), Guitar);

        await page.Find("[data-testid=score-profile]").ChangeAsync(new() { Value = "bass" });
        OptionValues(page, "score-tab-tuning").ShouldBe(["bass-4-standard", "bass-4-drop-d", "bass-5-standard"]);
        page.FindAll("[data-testid=score-tab-capo]").ShouldBeEmpty();

        await page.Find("[data-testid=score-profile]").ChangeAsync(new() { Value = "vocals" });
        page.FindAll("[data-testid=score-tab-layout]").ShouldBeEmpty();
        await page.Find("[data-testid=score-submit]").ClickAsync(new());
        SentRequest().Tablature.ShouldBeNull();
    }

    [Fact]
    public void NotAvailable_OrAnOlderBackend_ShowsNoTabOptions()
    {
        RenderDialogAt(ExperienceLevel.Expert, Options(available: false), Guitar).FindAll("[data-testid=score-tab-layout]").ShouldBeEmpty();
        RenderDialogAt(ExperienceLevel.Expert, ScoreData.Options("guitar", ScoreProfileDto.Guitar), Guitar)
            .FindAll("[data-testid=score-tab-layout]").ShouldBeEmpty();
    }

    [Fact]
    public void WithoutTheCatalogue_TheWireNamesAreShown()
    {
        Api.GetInstrumentCatalogueAsync(Arg.Any<CancellationToken>()).Returns<InstrumentCatalogueDto>(_ => throw new ApiException("down"));

        var page = RenderDialogAt(ExperienceLevel.Beginner, Options(), Guitar);

        page.Find("[data-testid=score-tab-tuning] option[value=guitar-drop-d]").TextContent.ShouldBe("guitar-drop-d");
    }

    [Fact]
    public void TheControls_HaveVisibleLabels_AndTheHelpAnchors()
    {
        var page = RenderDialogAt(ExperienceLevel.Advanced, Options(), Guitar);

        foreach (var (id, label) in new[] { ("score-tab-layout", "Layout"), ("score-tab-tuning", "Tuning"), ("score-tab-capo", "Capo") })
        {
            page.Find($"label[for={id}]").TextContent.ShouldBe(label);
            page.Find($"[data-testid={id}]").Id.ShouldBe(id);
        }

        page.Find("[data-testid=score-tab-layout]").GetAttribute("data-help-id").ShouldBe("score-tablature");
        page.Find("[data-testid=help-hint-tablature]").GetAttribute("aria-label").ShouldBe("Help: Tablature for guitar and bass");
    }
}
