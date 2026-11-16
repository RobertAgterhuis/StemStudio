using System.Net;
using Bunit;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Contracts.Generation;
using StemStudio.Contracts.Styles;
using StemStudio.Web.Api;
using StemStudio.Web.Components.Generation;
using StemStudio.Web.Components.Styles;
using StemStudio.Web.Styles;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Generation;

/// <summary>009 polish (T077): loading states, warnings, own-style details and the remaining keyboard paths.</summary>
public sealed class GenerativeEdgeTests : GenerationTestContext
{
    [Fact]
    public void WhileTheStatusLoads_TheBannerIsEmpty_AndRestyleIsDisabled()
    {
        Api.GetGenerationStatusAsync(Arg.Any<CancellationToken>()).Returns(new TaskCompletionSource<GenerationStatusDto>().Task);

        var section = Render<TrackGenerations>(p => p.Add(c => c.Track, TestData.Track()));

        section.FindAll("[data-testid=generation-status]").ShouldBeEmpty();
        section.Find("[data-testid=track-restyle]").HasAttribute("disabled").ShouldBeTrue();
    }

    [Fact]
    public void WhileListsAndStylesLoad_SkeletonsAreShown()
    {
        Api.ListGenerationsAsync(Arg.Any<GenerationQuery>(), Arg.Any<CancellationToken>()).Returns(new TaskCompletionSource<IReadOnlyList<GenerationDto>>().Task);
        Api.ListStylesAsync(Arg.Any<CancellationToken>()).Returns(new TaskCompletionSource<IReadOnlyList<StyleDto>>().Task);
        Api.GetStyleAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(new TaskCompletionSource<StyleDto>().Task);

        Render<GenerationList>(p => p.Add(c => c.Query, new GenerationQuery())).FindAll(".ss-skeleton").ShouldNotBeEmpty();
        Render<StylesPage>().FindAll(".ss-skeleton").ShouldNotBeEmpty();
        RenderDialog().FindAll(".ss-skeleton").ShouldNotBeEmpty();
        Render<StyleEditPage>(p => p.Add(c => c.Id, GenerationData.UserStyleId)).FindAll(".ss-skeleton").ShouldNotBeEmpty();
    }

    [Fact]
    public async Task AGenerationStartedBeforeTheListLoaded_IsShown()
    {
        Api.ListGenerationsAsync(Arg.Any<GenerationQuery>(), Arg.Any<CancellationToken>()).Returns(new TaskCompletionSource<IReadOnlyList<GenerationDto>>().Task);
        var list = Render<GenerationList>(p => p.Add(c => c.Query, new GenerationQuery()));

        await list.InvokeAsync(() => list.Instance.AddAsync(GenerationData.Generation(GenerationStateDto.Queued)));

        list.Find($"[data-testid=generation-row-{GenerationData.GenerationId}]").GetAttribute("data-status").ShouldBe("queued");
    }

    [Fact]
    public void Warnings_AreListedAsNotes()
    {
        var page = RenderList(GenerationData.Generation() with { Warnings = ["The range was shortened to the end of the track."] });

        page.Find($"[data-testid=generation-row-{GenerationData.GenerationId}] [role=note]").TextContent.ShouldBe("The range was shortened to the end of the track.");
    }

    [Fact]
    public void TakeDetails_OfAnOwnStyle_WithoutDevice()
    {
        var restyle = GenerationData.Restyle() with { Style = GenerationData.Snapshot(GenerationData.UserStyle()) };

        var page = RenderList(GenerationData.Generation() with { Restyle = restyle, Device = null });

        var details = page.Find($"[data-testid=take-details-{GenerationData.GenerationId}-1]").TextContent;
        details.ShouldContain("My bossa (own style)");
        details.ShouldNotContain("Device");
    }

    [Fact]
    public async Task OtherKeys_DoNotCloseOrAdd()
    {
        var dialog = RenderDialog();
        await dialog.Find("[data-testid=restyle-dialog]").KeyDownAsync(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "a" });
        Closed.ShouldBeFalse();

        var editor = Render<StyleNewPage>();
        await editor.Find("[data-testid=style-mood-tags]").InputAsync(new() { Value = "calm" });
        await editor.Find("[data-testid=style-mood-tags]").KeyDownAsync(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "m" });
        editor.FindAll("[data-testid=style-mood-tag-list] li").ShouldBeEmpty();
    }

    [Fact]
    public async Task DeleteFailure_InTheStyleLibrary_IsShown()
    {
        Api.DeleteStyleAsync(GenerationData.UserStyleId, Arg.Any<CancellationToken>())
            .ThrowsAsync(Problem(HttpStatusCode.Forbidden, "Preset is read-only", "Presets cannot be changed. Duplicate the preset to make your own version."));
        var page = Render<StylesPage>();

        await page.Find($"[data-testid=style-delete-{GenerationData.UserStyleId}]").ClickAsync(new());
        await page.Find("[data-testid=style-delete-confirm]").ClickAsync(new());

        page.Find("[data-testid=styles-action-error]").TextContent.ShouldStartWith("Presets cannot be changed.");
        page.FindAll($"[data-testid=style-row-{GenerationData.UserStyleId}]").Count.ShouldBe(1);
    }

    [Fact]
    public void EditorForm_FromAStyleWithoutSubGenre_SendsNull()
    {
        var form = StyleEditorForm.From(GenerationData.Synthwave());

        form.SubGenre.ShouldBe(string.Empty);
        form.ToInput().SubGenre.ShouldBeNull();
        form.ToInput().Description.ShouldBe("Retro synthesizers and a gated drum machine; instrumental.");
    }
}
