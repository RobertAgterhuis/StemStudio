using System.Net;
using Bunit;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Web.Components.Styles;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Styles;

/// <summary>Duplicate, edit, delete and new in the style library (009 T060, US3-1, US3-4; ui-test-ids 1.8.0).</summary>
public sealed class StyleActionsTests : PageTestContext
{
    [Fact]
    public void Presets_OnlyOfferDuplicate_OwnStylesAlsoEditAndDelete()
    {
        var page = Render<StylesPage>();

        page.FindAll($"[data-testid=style-duplicate-{GenerationData.JazzId}]").Count.ShouldBe(1);
        page.FindAll($"[data-testid=style-edit-{GenerationData.JazzId}]").ShouldBeEmpty();
        page.FindAll($"[data-testid=style-delete-{GenerationData.JazzId}]").ShouldBeEmpty();
        page.Find($"[data-testid=style-edit-{GenerationData.UserStyleId}]").GetAttribute("href").ShouldBe($"styles/{GenerationData.UserStyleId}/edit");
        page.FindAll($"[data-testid=style-delete-{GenerationData.UserStyleId}]").Count.ShouldBe(1);
        page.Find("[data-testid=style-new]").GetAttribute("href").ShouldBe("styles/new");
    }

    [Fact]
    public async Task Duplicate_OpensTheCopyInTheEditor()
    {
        var copy = GenerationData.UserStyle() with { Id = Guid.NewGuid(), Name = "Jazz trio (copy)" };
        Api.DuplicateStyleAsync(GenerationData.JazzId, Arg.Any<CancellationToken>()).Returns(copy);
        var page = Render<StylesPage>();

        await page.Find($"[data-testid=style-duplicate-{GenerationData.JazzId}]").ClickAsync(new());

        NavigatedTo.ShouldEndWith($"/styles/{copy.Id}/edit");
    }

    [Fact]
    public async Task Delete_AsksFirst_ThenRemovesTheRow()
    {
        var page = Render<StylesPage>();

        await page.Find($"[data-testid=style-delete-{GenerationData.UserStyleId}]").ClickAsync(new());
        page.Find("[role=dialog]").TextContent.ShouldContain("keep their copy of the style");
        await page.Find("[data-testid=style-delete-confirm]").ClickAsync(new());

        await Api.Received(1).DeleteStyleAsync(GenerationData.UserStyleId, Arg.Any<CancellationToken>());
        page.FindAll($"[data-testid=style-row-{GenerationData.UserStyleId}]").ShouldBeEmpty();
    }

    [Fact]
    public async Task Delete_CanBeCancelled_WithEscape()
    {
        var page = Render<StylesPage>();

        await page.Find($"[data-testid=style-delete-{GenerationData.UserStyleId}]").ClickAsync(new());
        await page.Find("[role=dialog]").KeyDownAsync(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Escape" });

        page.FindAll("[role=dialog]").ShouldBeEmpty();
        await Api.DidNotReceive().DeleteStyleAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ActionFailures_AreShown()
    {
        Api.DuplicateStyleAsync(GenerationData.JazzId, Arg.Any<CancellationToken>())
            .ThrowsAsync(Problem(HttpStatusCode.ServiceUnavailable, "The StemStudio API cannot be reached. Is it running?"));
        var page = Render<StylesPage>();

        await page.Find($"[data-testid=style-duplicate-{GenerationData.JazzId}]").ClickAsync(new());

        page.Find("[data-testid=styles-action-error]").TextContent.ShouldBe("The StemStudio API cannot be reached. Is it running?");
    }
}
