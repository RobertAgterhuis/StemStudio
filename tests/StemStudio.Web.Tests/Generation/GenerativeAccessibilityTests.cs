using AngleSharp.Dom;
using Bunit;
using NSubstitute;
using StemStudio.Contracts.Generation;
using StemStudio.Web.Components.Pages;
using StemStudio.Web.Components.Styles;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Generation;

/// <summary>
/// 009 polish (T077): disabled reasons are readable by screen readers, AI labels and states are text (not colour only),
/// every control of the restyle dialog has a name, and the confirmations are labelled modal dialogs.
/// </summary>
public sealed class GenerativeAccessibilityTests : GenerationTestContext
{
    private static void ShouldHaveAName(string markup, IElement control)
    {
        var named = control.HasAttribute("aria-label") || control.Closest("label") is not null
            || (control.Id is { Length: > 0 } id && markup.Contains($"for=\"{id}\"", StringComparison.Ordinal));
        named.ShouldBeTrue(control.GetAttribute("data-testid") ?? control.OuterHtml);
    }

    [Fact]
    public void DisabledRestyle_PointsAtAReasonThatIsOnThePage()
    {
        Api.GetGenerationStatusAsync(Arg.Any<CancellationToken>()).Returns(GenerationData.NotInstalled());
        Api.GetTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(TestData.Track());

        var page = Render<TrackDetail>(p => p.Add(c => c.Id, TestData.TrackId));

        var button = page.Find("[data-testid=track-restyle]");
        var reason = page.Find($"#{button.GetAttribute("aria-describedby")}");
        reason.TextContent.Trim().ShouldNotBeEmpty();
        reason.Closest("[role=status]").ShouldNotBeNull();
    }

    [Fact]
    public void Labels_AndStates_AreText()
    {
        var page = RenderList(GenerationData.Generation(), GenerationData.Generation(GenerationStateDto.Failed, GenerationData.OtherGenerationId));

        page.Find($"[data-testid=take-ai-badge-{GenerationData.GenerationId}-1]").TextContent.ShouldBe("AI-generated");
        page.FindAll(".ss-generation .ss-meta-status").Select(s => s.TextContent).ShouldBe(["Completed", "Failed"]);
        page.Find($"[data-testid=generation-progress-{GenerationData.GenerationId}]").GetAttribute("aria-label")
            .ShouldBe("Progress of Jazz trio: Never Gonna Give You Up");
    }

    [Fact]
    public void RestyleDialog_IsLabelled_AndEveryControlHasAName()
    {
        var page = RenderDialog();

        var dialog = page.Find("[role=dialog]");
        page.Find($"#{dialog.GetAttribute("aria-labelledby")}").TextContent.ShouldBe("Restyle");
        foreach (var control in dialog.QuerySelectorAll("input, select"))
        {
            ShouldHaveAName(page.Markup, control);
        }
    }

    [Fact]
    public async Task Confirmation_IsALabelledModal_ThatEscapeCloses()
    {
        var page = RenderList(GenerationData.Generation());

        await page.Find($"[data-testid=generation-delete-{GenerationData.GenerationId}]").ClickAsync(new());
        var dialog = page.Find("[role=dialog]");
        dialog.GetAttribute("aria-modal").ShouldBe("true");
        page.Find($"#{dialog.GetAttribute("aria-labelledby")}").TextContent.ShouldBe("Delete generation");
        page.Find($"#{dialog.GetAttribute("aria-describedby")}").TextContent.ShouldContain("imported tracks are kept");
        await dialog.KeyDownAsync(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Escape" });

        page.FindAll("[role=dialog]").ShouldBeEmpty();
        await Api.DidNotReceive().DeleteGenerationAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void StyleLibrary_FiltersAndRowActions_HaveNames()
    {
        var page = Render<StylesPage>();

        foreach (var control in page.FindAll("[data-testid^=style-filter-], [data-testid=style-search]"))
        {
            ShouldHaveAName(page.Markup, control);
        }

        page.Find($"[data-testid=style-duplicate-{GenerationData.JazzId}]").GetAttribute("aria-label").ShouldBe("Duplicate Jazz trio");
        page.Find($"[data-testid=style-delete-{GenerationData.UserStyleId}]").GetAttribute("aria-label").ShouldBe("Delete My bossa");
    }

    [Fact]
    public void StyleEditor_FieldsHaveNames()
    {
        var page = Render<StyleNewPage>();

        foreach (var control in page.FindAll("input, select, textarea"))
        {
            ShouldHaveAName(page.Markup, control);
        }
    }
}
