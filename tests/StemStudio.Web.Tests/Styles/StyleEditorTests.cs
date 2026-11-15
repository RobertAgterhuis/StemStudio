using System.Net;
using Bunit;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Contracts.Styles;
using StemStudio.Web.Api;
using StemStudio.Web.Components.Styles;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Styles;

/// <summary>The style editor (009 T060, US3-1..3, FR-004, FR-005; ui-test-ids 1.8.0 style editor ids).</summary>
public sealed class StyleEditorTests : PageTestContext
{
    public StyleEditorTests()
    {
        Api.CreateStyleAsync(Arg.Any<StyleInput>(), Arg.Any<CancellationToken>()).Returns(GenerationData.UserStyle());
        Api.UpdateStyleAsync(Arg.Any<Guid>(), Arg.Any<StyleInput>(), Arg.Any<CancellationToken>()).Returns(GenerationData.UserStyle());
        Api.GetStyleAsync(GenerationData.UserStyleId, Arg.Any<CancellationToken>()).Returns(GenerationData.UserStyle());
        Api.GetStyleAsync(GenerationData.JazzId, Arg.Any<CancellationToken>()).Returns(GenerationData.Jazz());
    }

    private StyleInput Sent(string method) =>
        (StyleInput)Api.ReceivedCalls().Single(c => c.GetMethodInfo().Name == method).GetArguments().OfType<StyleInput>().Single();

    private static async Task AddTag(IRenderedComponent<StyleNewPage> page, string testId, string tag)
    {
        await page.Find($"[data-testid={testId}]").InputAsync(new() { Value = tag });
        await page.Find($"[data-testid={testId}]").KeyDownAsync(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Enter" });
    }

    [Fact]
    public void NewStyle_HasEveryField_AsANativeControl_WithTheLimits()
    {
        var page = Render<StyleNewPage>();

        page.Find("h1").TextContent.ShouldBe("New style");
        page.Find("[data-help-id=styles-editor]").ShouldNotBeNull();
        page.Find("[data-testid=style-name-input]").GetAttribute("maxlength").ShouldBe("60");
        page.Find("[data-testid=style-subgenre-input]").GetAttribute("maxlength").ShouldBe("40");
        page.Find("[data-testid=style-description-input]").TagName.ShouldBe("TEXTAREA");
        page.Find("[data-testid=style-description-input]").GetAttribute("maxlength").ShouldBe("300");
        foreach (var id in new[] { "style-genre-select", "style-tempo-select", "style-vocal-select", "style-strength-select" })
        {
            page.Find($"[data-testid={id}]").TagName.ShouldBe("SELECT", id);
        }

        page.Find("[data-testid=style-genre-select]").QuerySelectorAll("option").Length.ShouldBe(14);
        page.Find("[data-testid=style-strength-select]").QuerySelectorAll("option").Select(o => o.TextContent)
            .ShouldBe(["Subtle", "Light", "Balanced", "Strong", "Free"]);
        page.Find("[data-testid=style-vocal-select]").QuerySelectorAll("option").Select(o => o.TextContent)
            .ShouldBe(["Keeps the vocals", "Instrumental (no vocals)"]);
        page.Find("[data-testid=style-mood-tags]").TagName.ShouldBe("INPUT");
    }

    [Fact]
    public async Task Save_CreatesTheStyle_WithTheTypedFields_AndGoesToTheLibrary()
    {
        var page = Render<StyleNewPage>();

        await page.Find("[data-testid=style-name-input]").InputAsync(new() { Value = "Night drive" });
        await page.Find("[data-testid=style-genre-select]").ChangeAsync(new() { Value = nameof(GenreFamilyDto.Electronic) });
        await page.Find("[data-testid=style-subgenre-input]").InputAsync(new() { Value = "Synth pop" });
        await page.Find("[data-testid=style-tempo-select]").ChangeAsync(new() { Value = nameof(TempoFeelDto.Driving) });
        await page.Find("[data-testid=style-vocal-select]").ChangeAsync(new() { Value = nameof(VocalHandlingDto.Instrumental) });
        await page.Find("[data-testid=style-strength-select]").ChangeAsync(new() { Value = nameof(StrengthDto.Strong) });
        await page.Find("[data-testid=style-description-input]").InputAsync(new() { Value = "Pulsing bass and wide pads." });
        await AddTag(page, "style-mood-tags", "dark");
        await AddTag(page, "style-instrument-tags", "analog synth");
        await page.Find("[data-testid=style-save]").ClickAsync(new());

        var input = Sent(nameof(IStemStudioApiClient.CreateStyleAsync));
        (input.Name, input.GenreFamily, input.SubGenre, input.TempoFeel, input.VocalHandling, input.DefaultStrength, input.Description).ShouldBe(
            ("Night drive", GenreFamilyDto.Electronic, "Synth pop", TempoFeelDto.Driving, VocalHandlingDto.Instrumental, StrengthDto.Strong, "Pulsing bass and wide pads."));
        (input.Moods, input.Instrumentation).ShouldBe((["dark"], ["analog synth"]));
        NavigatedTo.ShouldEndWith("/styles");
    }

    [Fact]
    public async Task Tags_AreListed_CanBeRemoved_AndStopAtEight()
    {
        var page = Render<StyleNewPage>();

        for (var i = 1; i <= 9; i++)
        {
            await AddTag(page, "style-mood-tags", $"mood {i}");
        }

        page.FindAll("[data-testid=style-mood-tag-list] li").Count.ShouldBe(8);
        page.Find("[data-testid=style-error]").GetAttribute("data-field").ShouldBe("moods");
        page.Find("[data-testid=style-error]").TextContent.ShouldBe("At most 8 tags.");
        await page.Find("[aria-label='Remove mood 1']").ClickAsync(new());
        page.FindAll("[data-testid=style-mood-tag-list] li").Count.ShouldBe(7);
    }

    [Theory]
    [InlineData("", "Enter a tag first.")]
    [InlineData("a very long instrument name that goes on", "Tags can be at most 30 characters.")]
    public async Task InvalidTags_AreRefused(string tag, string message)
    {
        var page = Render<StyleNewPage>();

        await AddTag(page, "style-instrument-tags", tag);

        page.Find("[data-testid=style-error]").GetAttribute("data-field").ShouldBe("instrumentation");
        page.Find("[data-testid=style-error]").TextContent.ShouldBe(message);
    }

    [Fact]
    public async Task ADuplicateTag_IsIgnored_AndTheAddButtonWorksToo()
    {
        var page = Render<StyleNewPage>();

        await AddTag(page, "style-mood-tags", "warm");
        await page.Find("[data-testid=style-mood-tags]").InputAsync(new() { Value = "Warm" });
        await page.Find("[data-testid=style-mood-tags-add]").ClickAsync(new());

        page.FindAll("[data-testid=style-mood-tag-list] li").Count.ShouldBe(1);
    }

    [Fact]
    public async Task MissingName_IsRefusedBeforeSending()
    {
        var page = Render<StyleNewPage>();

        await page.Find("[data-testid=style-save]").ClickAsync(new());

        var error = page.Find("[data-testid=style-error]");
        (error.GetAttribute("data-field"), error.TextContent).ShouldBe(("name", "Enter a name of 1 to 60 characters."));
        await Api.DidNotReceive().CreateStyleAsync(Arg.Any<StyleInput>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ImitationRefusal_IsShownWithItsField()
    {
        Api.CreateStyleAsync(Arg.Any<StyleInput>(), Arg.Any<CancellationToken>()).ThrowsAsync(new ApiException(new ApiError(
            HttpStatusCode.BadRequest, "Styles cannot imitate an artist.", null,
            new Dictionary<string, string[]> { ["description"] = ["Describe the music (genre, instruments, mood) instead of naming an artist or saying \"sounds like\"."] })));
        var page = Render<StyleNewPage>();

        await page.Find("[data-testid=style-name-input]").InputAsync(new() { Value = "Copycat" });
        await page.Find("[data-testid=style-save]").ClickAsync(new());

        var error = page.Find("[data-testid=style-error]");
        (error.GetAttribute("data-field"), error.GetAttribute("role")).ShouldBe(("description", "alert"));
        error.TextContent.ShouldContain("instead of naming an artist");
        NavigatedTo.ShouldNotEndWith("/styles");
    }

    [Fact]
    public async Task NameTaken_PointsAtTheName()
    {
        Api.CreateStyleAsync(Arg.Any<StyleInput>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(Problem(HttpStatusCode.Conflict, "Style name taken", "A style named \"Jazz trio\" already exists."));
        var page = Render<StyleNewPage>();

        await page.Find("[data-testid=style-name-input]").InputAsync(new() { Value = "Jazz trio" });
        await page.Find("[data-testid=style-save]").ClickAsync(new());

        var error = page.Find("[data-testid=style-error]");
        (error.GetAttribute("data-field"), error.TextContent).ShouldBe(("name", "A style named \"Jazz trio\" already exists."));
    }

    [Fact]
    public async Task OtherRefusals_HaveNoField()
    {
        Api.CreateStyleAsync(Arg.Any<StyleInput>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(Problem(HttpStatusCode.ServiceUnavailable, "The StemStudio API cannot be reached. Is it running?"));
        var page = Render<StyleNewPage>();

        await page.Find("[data-testid=style-name-input]").InputAsync(new() { Value = "Anything" });
        await page.Find("[data-testid=style-save]").ClickAsync(new());

        page.Find("[data-testid=style-error]").HasAttribute("data-field").ShouldBeFalse();
    }

    [Fact]
    public async Task EditPage_LoadsTheStyle_AndUpdatesIt()
    {
        var page = Render<StyleEditPage>(p => p.Add(c => c.Id, GenerationData.UserStyleId));

        page.Find("h1").TextContent.ShouldBe("Edit style");
        page.Find("[data-testid=style-name-input]").GetAttribute("value").ShouldBe("My bossa");
        page.Find("[data-testid=style-subgenre-input]").GetAttribute("value").ShouldBe("Bossa nova");
        page.FindAll("[data-testid=style-mood-tag-list] li").Select(li => li.TextContent.Trim()).ShouldBe(["dreamy"]);
        await page.Find("[data-testid=style-name-input]").InputAsync(new() { Value = "My bossa 2" });
        await page.Find("[data-testid=style-save]").ClickAsync(new());

        await Api.Received(1).UpdateStyleAsync(GenerationData.UserStyleId, Arg.Is<StyleInput>(i => i.Name == "My bossa 2" && i.Moods.SequenceEqual(new[] { "dreamy" })), Arg.Any<CancellationToken>());
        NavigatedTo.ShouldEndWith("/styles");
    }

    [Fact]
    public void EditPage_RefusesPresets_AndUnknownStyles()
    {
        var preset = Render<StyleEditPage>(p => p.Add(c => c.Id, GenerationData.JazzId));
        preset.Find("[data-testid=style-load-error]").TextContent.ShouldContain("Presets cannot be changed");
        preset.FindAll("[data-testid=style-save]").ShouldBeEmpty();

        Api.GetStyleAsync(GenerationData.SynthwaveId, Arg.Any<CancellationToken>()).ThrowsAsync(Problem(HttpStatusCode.NotFound, "Style not found"));
        var missing = Render<StyleEditPage>(p => p.Add(c => c.Id, GenerationData.SynthwaveId));
        missing.Find("[data-testid=style-load-error]").TextContent.ShouldBe("Style not found");
    }

    [Fact]
    public async Task Cancel_GoesBackToTheLibrary()
    {
        var page = Render<StyleNewPage>();

        await page.Find("[data-testid=style-cancel]").ClickAsync(new());

        NavigatedTo.ShouldEndWith("/styles");
        await Api.DidNotReceive().CreateStyleAsync(Arg.Any<StyleInput>(), Arg.Any<CancellationToken>());
    }
}
