using System.Net;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using NSubstitute;
using StemStudio.Contracts;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Handouts;
using StemStudio.Web.Components.Handouts;
using StemStudio.Web.Tests.Fakes;
using StemStudio.Web.Tests.InstrumentViews;

namespace StemStudio.Web.Tests.Handouts;

/// <summary>
/// "Customise..." handout dialog (T078, Advanced+, FR-008, FR-010, FR-013; ui-test-ids handout-*): ingredients with status
/// and create links, notation choices, transposition only for melodic, paper, estimate, notice, submit and cancel.
/// </summary>
public sealed class HandoutDialogTests : PageTestContext
{
    private static readonly HandoutSourceDto Song = HandoutSourceDto.Song(TestData.TrackId);
    private readonly List<(IngredientActionDto Action, HandoutFamilyDto Family)> _ingredientActions = [];
    private AssetDto? _created;
    private bool _closed;

    public HandoutDialogTests()
    {
        Api.GetHandoutOptionsAsync(Song, Arg.Any<HandoutFamilyDto>(), Arg.Any<TranspositionDto?>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<HandoutFamilyDto>(1) == HandoutFamilyDto.Melodic
                ? InsightsData.Options(HandoutFamilyDto.Melodic, "Melodic instruments")
                : InsightsData.BassOptions());
        Api.CreateHandoutAsync(Arg.Any<CreateHandoutRequest>(), Arg.Any<CancellationToken>()).Returns(InsightsData.Handout());
    }

    private IRenderedComponent<HandoutDialog> RenderDialog(bool withActions = true) => Render<HandoutDialog>(p =>
    {
        p.Add(c => c.Source, Song).Add(c => c.Family, HandoutFamilyDto.Bass)
            .Add(c => c.OnCreated, a => _created = a.Asset).Add(c => c.OnClose, () => _closed = true);
        if (withActions)
        {
            p.Add(c => c.OnIngredientAction, a => _ingredientActions.Add((a.Action, a.Family)));
        }
    });

    [Fact]
    public void Dialog_IsLabelled_WithFamilyPaperEstimateAndTheFixedNotice()
    {
        var dialog = RenderDialog();

        var root = dialog.Find("[data-testid=handout-dialog]");
        (root.GetAttribute("role"), root.GetAttribute("aria-modal")).ShouldBe(("dialog", "true"));
        dialog.Find($"#{root.GetAttribute("aria-labelledby")}").TextContent.ShouldBe("Customise the handout");
        dialog.Find("[data-testid=handout-family]").GetAttribute("value").ShouldBe("bass");
        dialog.Find("[data-testid=handout-paper]").GetAttribute("value").ShouldBe("a4");
        dialog.Find("[data-testid=handout-estimate]").TextContent.ShouldBe("Takes about 6 seconds.");
        dialog.Find("[data-testid=handout-notice]").TextContent.ShouldContain(ContractLimits.HandoutNoticeFooter);
        dialog.Find("[data-testid=handout-notice]").TextContent.ShouldContain("cannot be removed");
        dialog.FindAll("[data-testid=handout-transposition]").ShouldBeEmpty();
    }

    [Fact]
    public void Ingredients_ShowTheirStatus_AreCheckedByDefault_AndMissingOnesAreDisabled()
    {
        var dialog = RenderDialog();

        var facts = dialog.Find("[data-testid=handout-ingredient-facts]");
        (facts.GetAttribute("data-status"), facts.HasAttribute("checked"), facts.HasAttribute("disabled")).ShouldBe(("available", true, false));
        var notes = dialog.Find("[data-testid=handout-ingredient-practice-notes]");
        (notes.GetAttribute("data-status"), notes.HasAttribute("checked"), notes.HasAttribute("disabled")).ShouldBe(("missing", false, true));
        dialog.Find("label:has([data-testid=handout-ingredient-practice-notes])").TextContent.ShouldContain("Practice notes");
        dialog.Find("label:has([data-testid=handout-ingredient-practice-notes])").TextContent.ShouldContain("No notes yet");
        dialog.Find("[data-testid=handout-ingredient-action-notation]").TextContent.Trim().ShouldBe("Make tablature");
        dialog.Find("[data-testid=handout-ingredient-action-practice-notes]").TextContent.Trim().ShouldBe("Write practice notes");
        dialog.FindAll("[data-testid=handout-ingredient-action-facts]").ShouldBeEmpty();
    }

    [Fact]
    public async Task IngredientLink_ClosesTheDialog_AndReportsTheAction()
    {
        var dialog = RenderDialog();

        await dialog.Find("[data-testid=handout-ingredient-action-notation]").ClickAsync(new());

        _ingredientActions.ShouldBe([(IngredientActionDto.CreateTab, HandoutFamilyDto.Bass)]);
        _closed.ShouldBeTrue();
    }

    [Fact]
    public void WithoutAnActionHandler_NoCreateLinksAreShown()
    {
        var dialog = RenderDialog(withActions: false);

        dialog.FindAll("[data-testid^=handout-ingredient-action-]").ShouldBeEmpty();
    }

    [Fact]
    public void Notation_IsOfferedPerItem_SelectedByDefault()
    {
        var dialog = RenderDialog();

        var score = dialog.Find($"[data-testid=handout-notation-{InsightsData.BassScoreId}]");
        score.HasAttribute("checked").ShouldBeTrue();
        dialog.Find($"label:has([data-testid=handout-notation-{InsightsData.BassScoreId}])").TextContent.ShouldContain("Bass - score");
    }

    [Fact]
    public async Task Melodic_OffersTheTranspositions_AndReloadsTheOptionsForTheChosenOne()
    {
        var dialog = RenderDialog();

        dialog.Find("[data-testid=handout-family]").Change("melodic");
        dialog.FindAll("[data-testid=handout-transposition] option").Select(o => o.TextContent).ShouldBe(
            ["C (concert pitch)", "B-flat", "E-flat", "F"]);
        dialog.Find("[data-testid=handout-transposition]").Change("b-flat");

        await Api.Received(1).GetHandoutOptionsAsync(Song, HandoutFamilyDto.Melodic, TranspositionDto.BFlat, Arg.Any<CancellationToken>());
        dialog.Find("[data-testid=handout-transposition]").GetAttribute("value").ShouldBe("b-flat");
    }

    [Fact]
    public async Task Submit_SendsTheChoices_AndReportsTheCreatedItem()
    {
        var dialog = RenderDialog();
        dialog.Find("[data-testid=handout-ingredient-chord-chart]").Change(false);
        dialog.Find($"[data-testid=handout-notation-{InsightsData.BassScoreId}]").Change(false);
        dialog.Find("[data-testid=handout-paper]").Change("letter");

        await dialog.Find("[data-testid=handout-submit]").ClickAsync(new());

        await Api.Received(1).CreateHandoutAsync(Arg.Is<CreateHandoutRequest>(r =>
                r.Family == HandoutFamilyDto.Bass && r.Paper == PaperDto.Letter && r.Transposition == null &&
                r.Include!.SequenceEqual(new[] { IngredientCodeDto.Facts, IngredientCodeDto.FormChart, IngredientCodeDto.Notation }) &&
                r.NotationAssetIds!.Count == 0),
            Arg.Any<CancellationToken>());
        _created!.Id.ShouldBe(InsightsData.HandoutId);
    }

    [Fact]
    public async Task Submit_OfAMelodicPart_SendsTheTransposition_AndReCheckingAnIngredientIncludesItAgain()
    {
        var dialog = RenderDialog();
        dialog.Find("[data-testid=handout-family]").Change("melodic");
        dialog.Find("[data-testid=handout-transposition]").Change("e-flat");
        dialog.Find("[data-testid=handout-ingredient-facts]").Change(false);
        dialog.Find("[data-testid=handout-ingredient-facts]").Change(true);

        await dialog.Find("[data-testid=handout-submit]").ClickAsync(new());

        await Api.Received(1).CreateHandoutAsync(Arg.Is<CreateHandoutRequest>(r =>
            r.Family == HandoutFamilyDto.Melodic && r.Transposition == TranspositionDto.EFlat && r.Include!.Contains(IngredientCodeDto.Facts)), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SubmitError_IsShown_InTheDialog()
    {
        Api.CreateHandoutAsync(Arg.Any<CreateHandoutRequest>(), Arg.Any<CancellationToken>())
            .Returns<AssetDto>(_ => throw Problem(HttpStatusCode.Conflict, "Too many songs", "A setlist handout can hold up to 50 songs."));
        var dialog = RenderDialog();

        await dialog.Find("[data-testid=handout-submit]").ClickAsync(new());

        dialog.Find("[data-testid=handout-error]").TextContent.ShouldContain("up to 50 songs");
        _created.ShouldBeNull();
    }

    [Fact]
    public void OptionsError_IsShown_AndSubmitIsDisabled()
    {
        Api.GetHandoutOptionsAsync(Song, Arg.Any<HandoutFamilyDto>(), Arg.Any<TranspositionDto?>(), Arg.Any<CancellationToken>())
            .Returns<HandoutOptionsDto>(_ => throw Problem(HttpStatusCode.NotFound, "Track not found"));

        var dialog = RenderDialog();

        dialog.Find("[data-testid=handout-error]").TextContent.ShouldContain("Track not found");
        dialog.Find("[data-testid=handout-submit]").HasAttribute("disabled").ShouldBeTrue();
    }

    [Fact]
    public async Task Cancel_AndEscape_Close()
    {
        var dialog = RenderDialog();

        await dialog.Find("[data-testid=handout-cancel]").ClickAsync(new());
        _closed.ShouldBeTrue();
        _closed = false;
        await dialog.Find("[data-testid=handout-dialog]").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });
        _closed.ShouldBeTrue();
    }

    [Fact]
    public void Accessibility_GroupsHaveLegends_ControlsAreNamed()
    {
        var dialog = RenderDialog();

        dialog.FindAll("fieldset legend").Select(l => l.TextContent).ShouldBe(["Parts", "Sheet music"]);
        dialog.Find("label[for=handout-family]").TextContent.ShouldBe("Family");
        dialog.Find("label[for=handout-paper]").TextContent.ShouldBe("Paper");
        InstrumentViewsAccessibility.EveryControlIsNamedWithItsVisibleText(dialog.Find("[data-testid=handout-dialog]"));
    }
}
