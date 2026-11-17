using AngleSharp.Dom;
using Bunit;
using NSubstitute;
using StemStudio.Contracts.LibraryCare;
using StemStudio.Web.Components.Care;
using StemStudio.Web.Experience;
using StemStudio.Web.LibraryCare;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Care;

/// <summary>
/// 013 polish (T066, FR-032, WCAG 2.2 AA): the backup, restore, discard and clean-up dialogs and the Library care page are
/// usable with the keyboard (native controls, focus moved in and trapped, Escape) and with a screen reader (every dialog
/// and control named, ids unique, references resolve, tables with headers, ARIA only where a role allows it).
/// </summary>
public sealed class LibraryCareAccessibilityTests : PageTestContext
{
    private const string Row = LibraryCareData.Archive;

    public LibraryCareAccessibilityTests()
    {
        LibraryCare.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(LibraryCareData.Status(previous: LibraryCareData.Previous()));
        LibraryCare.GetStorageOverviewAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(StorageData.Overview());
        LibraryCare.PreviewCleanupAsync(Arg.Any<CleanupRequestDto>(), Arg.Any<CancellationToken>()).Returns(new CleanupPlanDto
        {
            PlanId = StorageData.PlanId,
            CreatedAt = StorageData.Measured,
            ExpiresAt = StorageData.Measured.AddMinutes(10),
            Items = [],
            Skipped = [],
            TotalBytes = 0,
        });
    }

    private IRenderedComponent<Microsoft.AspNetCore.Components.CascadingValue<ExperienceContext>> RenderPage() =>
        RenderAtLevel<LibraryCarePage>(ExperienceLevel.Expert, _ => { });

    private static void IsAccessible(IRenderedComponent<Microsoft.AspNetCore.Components.CascadingValue<ExperienceContext>> page, IElement root)
    {
        var markup = page.Markup;
        foreach (var control in root.QuerySelectorAll("input, select, textarea"))
        {
            var named = control.HasAttribute("aria-label") || control.Closest("label") is not null
                || (control.Id is { Length: > 0 } id && markup.Contains($"for=\"{id}\"", StringComparison.Ordinal));
            named.ShouldBeTrue(control.GetAttribute("data-testid") ?? control.OuterHtml);
        }

        foreach (var button in root.QuerySelectorAll("button"))
        {
            (button.GetAttribute("aria-label") ?? button.TextContent).Trim().ShouldNotBeEmpty(button.OuterHtml);
        }

        foreach (var reference in root.QuerySelectorAll("[aria-labelledby], [aria-describedby], [aria-controls]"))
        {
            foreach (var id in new[] { "aria-labelledby", "aria-describedby", "aria-controls" }.Select(reference.GetAttribute).OfType<string>())
            {
                page.FindAll($"[id='{id}']").Count.ShouldBe(1, $"{reference.GetAttribute("data-testid")} -> {id}");
            }
        }
    }

    private static void IsATrappedModal(IElement dialog, string role = "dialog")
    {
        (dialog.GetAttribute("role"), dialog.GetAttribute("aria-modal")).ShouldBe((role, "true"));
        dialog.QuerySelectorAll("[data-focus-sentinel]").Length.ShouldBe(3);
        dialog.QuerySelector("[tabindex='-1'].ss-title").ShouldNotBeNull();
    }

    [Fact]
    public void Page_IdsAreUnique_TablesHaveHeaders_AndNoAriaWithoutARole()
    {
        var page = RenderPage();

        var ids = page.FindAll("[id]").Select(e => e.Id).ToList();
        ids.Distinct(StringComparer.Ordinal).Count().ShouldBe(ids.Count);
        page.FindAll("table").ShouldAllBe(t => t.QuerySelectorAll("th[scope=col]").Length > 0);
        IsAccessible(page, page.Find("[data-testid=care-page]"));
        page.FindAll("[data-testid=care-page] [aria-valuetext]:not([role])").ShouldBeEmpty();
    }

    [Fact]
    public async Task StorageTab_IdsAreUnique_AndMetersAndScrollRegionsAreNamed()
    {
        LibraryCare.GetStorageOverviewAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(StorageData.Overview(workerAvailable: false) with
        {
            Volumes = [StorageData.Volume(StorageVolumeIdDto.Library), StorageData.Volume(StorageVolumeIdDto.WorkerData, null, null, reason: "down")],
        });
        var page = RenderPage();

        await page.Find("[data-testid=care-tab-storage]").ClickAsync(new());

        var ids = page.FindAll("[id]").Select(e => e.Id).ToList();
        ids.Distinct(StringComparer.Ordinal).Count().ShouldBe(ids.Count);
        page.FindAll("[role=meter]").ShouldAllBe(m => m.HasAttribute("aria-label") && m.HasAttribute("aria-valuenow"));
        var unmeasured = page.Find("[data-testid=storage-volume-worker-data]");
        (unmeasured.HasAttribute("aria-label"), unmeasured.HasAttribute("aria-valuetext")).ShouldBe((false, false));
        page.FindAll("[role=region][tabindex='0']").ShouldAllBe(r => r.HasAttribute("aria-label") || r.HasAttribute("aria-labelledby"));
        IsAccessible(page, page.Find("[data-testid=care-page]"));
    }

    [Fact]
    public async Task BackupDialog_IsAKeyboardModal_WithNamedControls()
    {
        var page = RenderPage();

        await page.Find("[data-testid=backup-new]").ClickAsync(new());

        var dialog = page.Find("[data-testid=backup-dialog]");
        IsATrappedModal(dialog);
        IsAccessible(page, dialog);
        dialog.QuerySelectorAll("fieldset > legend").Length.ShouldBe(2);
        dialog.QuerySelectorAll("input[type=radio]").ShouldAllBe(r => r.GetAttribute("name") == "backup-profile");
    }

    [Fact]
    public async Task RestoreDialog_IsAKeyboardModal_WithNamedControls()
    {
        var page = RenderPage();

        await page.Find($"[data-testid='backup-restore-{Row}']").ClickAsync(new());

        var dialog = page.Find("[data-testid=restore-dialog]");
        IsATrappedModal(dialog);
        IsAccessible(page, dialog);
        page.Find("label[for=restore-confirm-input]").TextContent.ShouldContain("REPLACE");
        dialog.QuerySelector("fieldset > legend")!.TextContent.ShouldBe("How to restore");
    }

    [Fact]
    public async Task DiscardDialog_IsAKeyboardModal_WithALabelledInput()
    {
        var page = RenderPage();

        await page.Find("[data-testid=previous-library-discard]").ClickAsync(new());

        var dialog = page.Find("[role=dialog]");
        IsATrappedModal(dialog);
        IsAccessible(page, dialog);
        page.Find("label[for=previous-library-discard-input]").TextContent.ShouldContain("DISCARD");
    }

    [Fact]
    public async Task CleanupDialog_IsAKeyboardModal()
    {
        var page = RenderPage();
        await page.Find("[data-testid=care-tab-storage]").ClickAsync(new());

        await page.Find("[data-testid=cleanup-leftovers]").ClickAsync(new());

        var dialog = page.Find("[data-testid=cleanup-dialog]");
        IsATrappedModal(dialog);
        IsAccessible(page, dialog);
    }

    [Fact]
    public void MaintenanceOverlay_IsAnAlertDialog_WithATrap()
    {
        var overlay = Render<StemStudio.Web.Components.Layout.MaintenanceOverlay>().Find("[data-testid=library-maintenance]");

        IsATrappedModal(overlay, "alertdialog");
    }

    [Fact]
    public void CareCss_KeepsTargetsAndFocusVisible()
    {
        var css = RepoPaths.ReadWeb("wwwroot/css/components/care.css");

        css.ShouldContain("min-height: max(var(--ss-target-min), 2.75rem)");
        css.ShouldContain(".ss-care-tab-button:focus-visible");
        css.ShouldContain(".ss-care-sort:focus-visible");
        css.ShouldContain("html[data-motion=\"reduced\"] .ss-care-progress-fill");
        RepoPaths.ReadWeb("Components/App.razor").ShouldContain("\"care\"]");
        StorageView.MeterAttributes(new VolumeLine(StorageData.Volume(StorageVolumeIdDto.Backups, null, null), []), "x").ShouldBeEmpty();
    }
}
