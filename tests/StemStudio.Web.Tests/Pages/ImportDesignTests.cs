using Bunit;
using Microsoft.AspNetCore.Components;
using NSubstitute;
using StemStudio.Web.Components.Pages;
using StemStudio.Web.Experience;
using StemStudio.Web.Preferences;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Pages;

/// <summary>Import screen in the design system (T027, FR-005, FR-029; ui-test-ids 1.3.0 R7/R8).</summary>
public sealed class ImportDesignTests : PageTestContext
{
    private IRenderedComponent<CascadingValue<ExperienceContext>> RenderAt(ExperienceLevel level) =>
        Render<CascadingValue<ExperienceContext>>(p => p
            .Add(c => c.Value, new ExperienceContext(level, DensityPreference.Comfortable))
            .AddChildContent<Import>());

    public static TheoryData<ExperienceLevel> Levels => [ExperienceLevel.Beginner, ExperienceLevel.Advanced, ExperienceLevel.Expert];

    [Theory]
    [MemberData(nameof(Levels))]
    public void EveryImportId_IsOnItsElement_AtEveryLevel(ExperienceLevel level)
    {
        var page = RenderAt(level);

        page.Find("[data-testid=import-url-input]").TagName.ShouldBe("INPUT");
        page.Find("[data-testid=import-url-submit]").TagName.ShouldBe("BUTTON");
        page.Find("[data-testid=import-file-input]").GetAttribute("type").ShouldBe("file");
        page.Find("[data-testid=worker-device]").ShouldNotBeNull();
        page.Find("[data-testid=legal-notice]").TextContent.ShouldContain("for personal practice only");
        page.Find("[data-testid=stem-limitation-help]").TextContent.ShouldContain("not reliably possible with current open-source models");
    }

    [Fact]
    public void Title_IsTheOnlyH1()
    {
        var page = RenderAt(ExperienceLevel.Advanced);

        page.FindAll("h1").Single().TextContent.ShouldBe("Import a song");
    }

    [Fact]
    public void UrlField_HasAVisibleLabel_AndThePrimaryImportButton()
    {
        var page = RenderAt(ExperienceLevel.Advanced);

        var input = page.Find("[data-testid=import-url-input]");
        page.Find($"label[for={input.Id}]").TextContent.ShouldContain("YouTube video or Spotify track link");
        page.Find("[data-testid=import-url-submit]").ClassList.ShouldContain("rz-primary");
    }

    [Fact]
    public void DropZone_WrapsTheFileInput_WithInstructions()
    {
        var page = RenderAt(ExperienceLevel.Advanced);

        var zone = page.Find(".ss-dropzone");
        zone.QuerySelector("[data-testid=import-file-input]").ShouldNotBeNull();
        zone.TextContent.ShouldContain("Drop an MP3, WAV or FLAC file here");
        zone.TextContent.ShouldContain("200 MiB");
    }

    [Fact]
    public void LegalNotice_AndLimitationNote_AreNotes_InTheirOwnCards()
    {
        var page = RenderAt(ExperienceLevel.Beginner);

        page.Find("[data-testid=legal-notice]").GetAttribute("role").ShouldBe("note");
        page.Find("[data-testid=stem-limitation-help]").ClosestElement(".ss-limitation").ShouldNotBeNull();
    }

    [Fact]
    public async Task Error_IsShownAsADesignedErrorState()
    {
        var page = RenderAt(ExperienceLevel.Advanced);

        await page.Find("form").SubmitAsync();

        var error = page.Find("[data-testid=import-error]");
        error.GetAttribute("role").ShouldBe("alert");
        error.ClosestElement(".ss-state-error").ShouldNotBeNull();
    }

    [Fact]
    public void DeviceLine_IsPartOfTheForm()
    {
        Api.GetDeviceAsync(Arg.Any<CancellationToken>()).Returns(StemStudio.Contracts.Health.ProcessingDeviceDto.Unreachable);

        var page = RenderAt(ExperienceLevel.Advanced);

        page.Find("[data-testid=worker-device]").ClosestElement(".ss-import").ShouldNotBeNull();
    }
}

internal static class ElementExtensions
{
    public static AngleSharp.Dom.IElement? ClosestElement(this AngleSharp.Dom.IElement element, string selector) => element.Closest(selector);
}
