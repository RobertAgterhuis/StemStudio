using System.Net;
using Bunit;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Contracts.Generation;
using StemStudio.Contracts.Styles;
using StemStudio.Web.Experience;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Generation;

/// <summary>The restyle dialog (009 T043, FR-001, FR-002, FR-015, FR-016; ui-test-ids 1.8.0).</summary>
public sealed class RestyleDialogTests : GenerationTestContext
{
    [Fact]
    public void Dialog_IsAModal_ForTheTrack_WithItsHelpMarker()
    {
        var page = RenderDialog();

        var dialog = page.Find("[data-testid=restyle-dialog]");
        (dialog.GetAttribute("role"), dialog.GetAttribute("aria-modal"), dialog.GetAttribute("data-track-id"))
            .ShouldBe(("dialog", "true", TestData.TrackId.ToString()));
        dialog.GetAttribute("data-help-id").ShouldBe("restyle-options");
        page.Find("[data-testid=help-hint-restyle-options]").ShouldNotBeNull();
    }

    [Fact]
    public void StyleSelect_ListsEveryStyle_AndDescribesTheSelectedOne()
    {
        var page = RenderDialog();

        var select = page.Find("[data-testid=restyle-style-select]");
        select.TagName.ShouldBe("SELECT");
        select.QuerySelectorAll("option").Select(o => o.TextContent).ShouldBe(["Jazz trio", "Synthwave", "My bossa"]);
        page.Find("[data-testid=restyle-style-description]").TextContent.ShouldBe("Acoustic piano trio with brushed drums and a walking bass.");
    }

    [Fact]
    public async Task ChoosingAStyle_ShowsItsDescriptionAndDefaultStrength()
    {
        var page = RenderDialog();

        await page.Find("[data-testid=restyle-style-select]").ChangeAsync(new() { Value = GenerationData.SynthwaveId.ToString() });

        page.Find("[data-testid=restyle-style-description]").TextContent.ShouldBe("Retro synthesizers and a gated drum machine; instrumental.");
        var strength = page.Find("[data-testid=restyle-strength]");
        (strength.GetAttribute("aria-valuenow"), strength.GetAttribute("aria-valuetext")).ShouldBe(("3", "Strong"));
        var vocals = page.Find("[data-testid=restyle-stem-vocals]");
        vocals.HasAttribute("disabled").ShouldBeTrue();
        vocals.HasAttribute("checked").ShouldBeFalse();
    }

    [Fact]
    public async Task AStyleWithoutDescription_SaysSo()
    {
        var page = RenderDialog();

        await page.Find("[data-testid=restyle-style-select]").ChangeAsync(new() { Value = GenerationData.UserStyleId.ToString() });

        page.Find("[data-testid=restyle-style-description]").TextContent.ShouldBe("This style has no description.");
    }

    [Fact]
    public void Options_AreNativeControls_WithTheLimits()
    {
        var page = RenderDialog();

        var start = page.Find("[data-testid=restyle-range-start]");
        (start.GetAttribute("type"), start.GetAttribute("value")).ShouldBe(("number", "0"));
        page.Find("[data-testid=restyle-range-end]").GetAttribute("value").ShouldBe("213.04");
        var strength = page.Find("[data-testid=restyle-strength]");
        (strength.GetAttribute("type"), strength.GetAttribute("min"), strength.GetAttribute("max")).ShouldBe(("range", "0", "4"));
        (strength.GetAttribute("aria-valuenow"), strength.GetAttribute("aria-valuetext")).ShouldBe(("2", "Balanced"));
        strength.GetAttribute("data-help-id").ShouldBe("restyle-strength");
        var takes = page.Find("[data-testid=restyle-takes]");
        (takes.GetAttribute("min"), takes.GetAttribute("max"), takes.GetAttribute("value")).ShouldBe(("1", "2", "1"));
        page.FindAll("[data-testid^=restyle-stem-]").Select(e => e.GetAttribute("data-testid"))
            .ShouldBe(["restyle-stem-vocals", "restyle-stem-drums", "restyle-stem-bass", "restyle-stem-guitar", "restyle-stem-piano", "restyle-stem-other"]);
    }

    [Fact]
    public void Notice_SaysPersonalUse_AiGenerated_AndNoImitation()
    {
        var page = RenderDialog();

        var notice = page.Find("[data-testid=restyle-personal-use-notice]");
        notice.GetAttribute("data-help-id").ShouldBe("generation-personal-use");
        notice.TextContent.ShouldContain("personal use");
        notice.TextContent.ShouldContain("AI-generated");
        notice.TextContent.ShouldContain("imitate");
    }

    [Fact]
    public async Task Start_SendsTheChoices_AndReportsTheGeneration()
    {
        var page = RenderDialog();

        await page.Find("[data-testid=restyle-range-start]").ChangeAsync(new() { Value = "2" });
        await page.Find("[data-testid=restyle-range-end]").ChangeAsync(new() { Value = "14" });
        await page.Find("[data-testid=restyle-stem-vocals]").ChangeAsync(new() { Value = false });
        await page.Find("[data-testid=restyle-strength]").InputAsync(new() { Value = "1" });
        await page.Find("[data-testid=restyle-takes]").ChangeAsync(new() { Value = "2" });
        await page.Find("[data-testid=restyle-start]").ClickAsync(new());

        var request = SentRestyle();
        (request.Mode, request.RangeStartSeconds, request.RangeEndSeconds, request.Strength, request.Takes)
            .ShouldBe((RestyleModeDto.Range, 2d, 14d, StrengthDto.Light, 2));
        request.IncludedStems!.ShouldNotContain("vocals");
        Started!.Status.ShouldBe(GenerationStateDto.Queued);
        page.Find("[data-testid=restyle-strength]").GetAttribute("aria-valuetext").ShouldBe("Light");
    }

    [Fact]
    public async Task InvalidChoices_AreShown_AndNothingIsSent()
    {
        var page = RenderDialog();

        await page.Find("[data-testid=restyle-range-end]").ChangeAsync(new() { Value = "5" });
        await page.Find("[data-testid=restyle-start]").ClickAsync(new());

        page.Find("[data-testid=restyle-error]").TextContent.ShouldBe("The range must be 10 seconds to 8 minutes long.");
        await Api.DidNotReceive().CreateRestyleAsync(Arg.Any<Guid>(), Arg.Any<RestyleRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApiRefusal_IsShownVerbatim()
    {
        Api.CreateRestyleAsync(Arg.Any<Guid>(), Arg.Any<RestyleRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(Problem(HttpStatusCode.Conflict, "GPU required", "No NVIDIA GPU is available to the generator (CUDA is not available)."));
        var page = RenderDialog();

        await page.Find("[data-testid=restyle-start]").ClickAsync(new());

        page.Find("[data-testid=restyle-error]").TextContent.ShouldBe("No NVIDIA GPU is available to the generator (CUDA is not available).");
        Started.ShouldBeNull();
    }

    [Fact]
    public void StylesThatCannotBeLoaded_AreReported()
    {
        Api.ListStylesAsync(Arg.Any<CancellationToken>()).ThrowsAsync(Problem(HttpStatusCode.ServiceUnavailable, "The StemStudio API cannot be reached. Is it running?"));

        var page = RenderDialog();

        page.Find("[data-testid=restyle-error]").TextContent.ShouldBe("The StemStudio API cannot be reached. Is it running?");
        page.FindAll("[data-testid=restyle-start]").ShouldBeEmpty();
    }

    [Fact]
    public async Task CancelAndEscape_Close()
    {
        var page = RenderDialog();

        await page.Find("[data-testid=restyle-cancel]").ClickAsync(new());
        Closed.ShouldBeTrue();

        var again = RenderDialog();
        await again.Find("[data-testid=restyle-dialog]").KeyDownAsync(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Escape" });
        Closed.ShouldBeTrue();
    }

    [Fact]
    public void Beginner_SeesStyleNoticeAndStart_ButNoOptions()
    {
        var page = RenderDialogAt(ExperienceLevel.Beginner);

        foreach (var id in new[] { "restyle-style-select", "restyle-style-description", "restyle-personal-use-notice", "restyle-start", "restyle-cancel" })
        {
            page.FindAll($"[data-testid={id}]").Count.ShouldBe(1, id);
        }

        foreach (var id in new[] { "restyle-range-start", "restyle-range-end", "restyle-stem-vocals", "restyle-strength", "restyle-takes" })
        {
            page.FindAll($"[data-testid={id}]").ShouldBeEmpty(id);
        }
    }
}
