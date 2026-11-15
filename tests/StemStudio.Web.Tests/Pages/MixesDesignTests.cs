using System.Net;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Components.Mixer;
using StemStudio.Web.Components.Pages;
using StemStudio.Web.Components.Shared;
using StemStudio.Web.Experience;
using StemStudio.Web.Preferences;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Pages;

/// <summary>Mix library and dialogs in the design system (T035, FR-005).</summary>
public sealed class MixesDesignTests : PageTestContext
{
    private static readonly Guid OtherId = Guid.Parse("12121212-3434-5656-7878-909090909090");
    private static readonly Guid UnknownTrack = Guid.Parse("99999999-3434-5656-7878-909090909090");

    public MixesDesignTests()
    {
        Api.ListMixesAsync(null, Arg.Any<CancellationToken>()).Returns(
        [
            TestData.Summary(),
            TestData.Summary(OtherId, "Drums only") with { TrackId = UnknownTrack, TrackTitle = "Gone song" },
        ]);
        Api.ListTracksAsync(Arg.Any<CancellationToken>()).Returns([TestData.Track()]);
    }

    private IRenderedComponent<CascadingValue<ExperienceContext>> RenderAt(ExperienceLevel level) =>
        Render<CascadingValue<ExperienceContext>>(p => p
            .Add(c => c.Value, new ExperienceContext(level, DensityPreference.Comfortable))
            .AddChildContent<Mixes>());

    public static TheoryData<ExperienceLevel> Levels => [ExperienceLevel.Beginner, ExperienceLevel.Advanced, ExperienceLevel.Expert];

    [Theory]
    [MemberData(nameof(Levels))]
    public void EveryActionId_IsInline_AtEveryLevel(ExperienceLevel level)
    {
        var page = RenderAt(level);

        foreach (var action in new[] { "load", "duplicate", "rename", "delete" })
        {
            page.Find($"[data-testid=mix-row-{TestData.MixId}] [data-testid=mix-{action}-{TestData.MixId}]").TagName.ShouldBe("BUTTON");
        }
    }

    [Fact]
    public void Rows_ShowTheTrackRibbon_FromTheTrackList()
    {
        var page = RenderAt(ExperienceLevel.Advanced);

        page.Find($"[data-testid=mix-ribbon-{TestData.MixId}]").GetAttribute("data-stems").ShouldBe("vocals,drums,bass,guitar,piano,other");
        page.Find($"[data-testid=mix-ribbon-{OtherId}]").GetAttribute("data-stems").ShouldBe("");
    }

    [Fact]
    public void Ribbons_AreEmpty_WhenTheTrackListFails()
    {
        Api.ListTracksAsync(Arg.Any<CancellationToken>()).ThrowsAsync(Problem(HttpStatusCode.ServiceUnavailable, "down"));

        var page = RenderAt(ExperienceLevel.Advanced);

        page.Find($"[data-testid=mix-ribbon-{TestData.MixId}]").GetAttribute("data-stems").ShouldBe("");
        page.FindAll("[data-testid=mixes-error]").ShouldBeEmpty();
    }

    [Fact]
    public void Load_IsThePrimaryAction()
    {
        var page = RenderAt(ExperienceLevel.Advanced);

        page.Find($"[data-testid=mix-load-{TestData.MixId}]").ClassList.ShouldContain("rz-primary");
        page.Find($"[data-testid=mix-delete-{TestData.MixId}]").ClassList.ShouldContain("rz-danger");
    }

    [Fact]
    public void Expert_UsesIconButtons_WithAccessibleNamesAndVisibleTooltips()
    {
        var page = RenderAt(ExperienceLevel.Expert);

        var duplicate = page.Find($"[data-testid=mix-duplicate-{TestData.MixId}]");
        duplicate.GetAttribute("aria-label").ShouldBe("Duplicate Guitar practice");
        duplicate.GetAttribute("data-tooltip").ShouldBe("Duplicate");
        duplicate.ClassList.ShouldContain("ss-icon-only");
    }

    [Fact]
    public void Advanced_ShowsTextButtons_WithNamesIncludingTheMix()
    {
        var page = RenderAt(ExperienceLevel.Advanced);

        var rename = page.Find($"[data-testid=mix-rename-{TestData.MixId}]");
        rename.TextContent.ShouldContain("Rename");
        rename.GetAttribute("aria-label").ShouldBe("Rename Guitar practice");
        rename.ClassList.ShouldNotContain("ss-icon-only");
    }

    [Fact]
    public void Beginner_HasNoSearch()
    {
        RenderAt(ExperienceLevel.Beginner).FindAll("[data-testid=mixes-search]").ShouldBeEmpty();
    }

    [Fact]
    public void Search_FiltersByMixNameOrTrackTitle_AtAdvanced()
    {
        var page = RenderAt(ExperienceLevel.Advanced);

        page.Find("[data-testid=mixes-search]").Input("gone");

        page.FindAll($"[data-testid=mix-row-{TestData.MixId}]").ShouldBeEmpty();
        page.Find($"[data-testid=mix-row-{OtherId}]").ShouldNotBeNull();

        page.Find("[data-testid=mixes-search]").Input("zzz");
        page.Find(".ss-no-results").TextContent.ShouldContain("No mixes match");
    }

    [Fact]
    public void Empty_IsADesignedEmptyState()
    {
        Api.ListMixesAsync(null, Arg.Any<CancellationToken>()).Returns([]);

        var page = RenderAt(ExperienceLevel.Advanced);

        var empty = page.Find("[data-testid=mixes-empty]");
        empty.ClassList.ShouldContain("ss-state-empty");
        empty.TextContent.ShouldContain("No saved mixes yet.");
    }

    [Fact]
    public void Error_IsADesignedErrorState()
    {
        Api.ListMixesAsync(null, Arg.Any<CancellationToken>()).ThrowsAsync(Problem(HttpStatusCode.ServiceUnavailable, "The API is down."));

        var page = RenderAt(ExperienceLevel.Advanced);

        page.Find("[data-testid=mixes-error]").TextContent.ShouldBe("The API is down.");
        page.Find("[data-testid=mixes-error]").Closest(".ss-state-error").ShouldNotBeNull();
    }

    [Fact]
    public void SaveMixDialog_IsAModalDialog_WithItsIds_AndCapturesFocus()
    {
        var dialog = Render<SaveMixDialog>(p => p.Add(d => d.Name, "Practice"));

        var box = dialog.Find("[role=dialog]");
        box.GetAttribute("aria-modal").ShouldBe("true");
        box.GetAttribute("aria-labelledby").ShouldBe("save-mix-title");
        dialog.Find("[data-testid=mix-name-input]").GetAttribute("value").ShouldBe("Practice");
        dialog.Find("[data-testid=mix-save-confirm]").ClassList.ShouldContain("rz-primary");
        dialog.Find("[data-testid=mix-save-cancel]").ShouldNotBeNull();
        Interop.FocusCaptures.ShouldBe(1);
    }

    [Fact]
    public async Task SaveMixDialog_Escape_Cancels_AndRestoresFocusWhenClosed()
    {
        var cancelled = false;
        var dialog = Render<SaveMixDialog>(p => p.Add(d => d.OnCancel, () => cancelled = true));

        await dialog.Find("[role=dialog]").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });
        await dialog.Find("[role=dialog]").KeyDownAsync(new KeyboardEventArgs { Key = "a" });
        await DisposeComponentsAsync();

        cancelled.ShouldBeTrue();
        Interop.FocusRestores.ShouldBe(1);
    }

    [Fact]
    public async Task SaveMixDialog_EnterInTheNameField_Confirms_UnlessBusy()
    {
        // SC-008 keyboard flow "Mod+S, name, Enter" (T067: Enter did nothing).
        var confirmed = new List<string>();
        var dialog = Render<SaveMixDialog>(p => p.Add(d => d.OnConfirm, (string name) => confirmed.Add(name)));
        var input = dialog.Find("[data-testid=mix-name-input]");

        await input.InputAsync(new ChangeEventArgs { Value = "Groove" });
        await dialog.Find("[data-testid=mix-name-input]").KeyDownAsync(new KeyboardEventArgs { Key = "a" });
        await dialog.Find("[data-testid=mix-name-input]").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });
        dialog.Render(p => p.Add(d => d.Busy, true));
        await dialog.Find("[data-testid=mix-name-input]").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        confirmed.ShouldBe(["Groove"]);
    }

    [Fact]
    public void ConfirmDialog_HasItsIds_CancelFirst_DestructiveLast()
    {
        var dialog = Render<ConfirmDialog>(p => p.Add(d => d.Message, "Delete it?").Add(d => d.OkText, "Delete"));

        dialog.Find("[data-testid=confirm-dialog-message]").TextContent.ShouldBe("Delete it?");
        var buttons = dialog.FindAll(".ss-dialog-actions button").Select(b => b.GetAttribute("data-testid")).ToList();
        buttons.ShouldBe(["confirm-dialog-cancel", "confirm-dialog-ok"]);
        dialog.Find("[data-testid=confirm-dialog-ok]").ClassList.ShouldContain("rz-danger");
    }

    [Fact]
    public void Ribbons_IgnoreTracksThatAreNotListed()
    {
        Api.ListTracksAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<TrackDto>());

        var page = RenderAt(ExperienceLevel.Advanced);

        page.Find($"[data-testid=mix-ribbon-{TestData.MixId}]").GetAttribute("data-stems").ShouldBe("");
    }
}
