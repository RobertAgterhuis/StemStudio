using System.Net;
using Bunit;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Contracts.LibraryCare;
using StemStudio.Web.Api;
using StemStudio.Web.Components.Care;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Care;

/// <summary>
/// The previous library, restore from the Backups tab and the restore report (013 T052, US2, FR-012-FR-018, FR-026;
/// ui-test-ids 1.12.0 previous-library*, restore-*, library-job-report).
/// </summary>
public sealed class PreviousLibraryTests : PageTestContext
{
    private const string Row = LibraryCareData.Archive;

    public PreviousLibraryTests()
    {
        LibraryCare.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(LibraryCareData.Status(previous: LibraryCareData.Previous()));
        LibraryCare.GetStorageOverviewAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(StorageData.Overview());
    }

    private IRenderedComponent<LibraryCarePage> RenderPage() => Render<LibraryCarePage>();

    private static AngleSharp.Dom.IElement Id(IRenderedComponent<LibraryCarePage> page, string id) => page.Find($"[data-testid='{id}']");

    private static ApiException Refused(HttpStatusCode status, string code, string detail) =>
        new(new ApiError(status, "Refused", detail, new Dictionary<string, string[]>()) { Type = $"https://stemstudio.local/problems/{code}" });

    [Fact]
    public void Panel_ShowsWhenSizeAndCounts_WithHelp()
    {
        var panel = Id(RenderPage(), "previous-library");

        panel.GetAttribute("data-help-id").ShouldBe("previous-library");
        panel.TextContent.ShouldContain($"Kept aside on 17 Nov 2026, 10:15 (restored {Row}): 5.0 GB, 7 tracks, 1 mix");
    }

    [Fact]
    public void NoPreviousLibrary_NoPanel()
    {
        LibraryCare.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(LibraryCareData.Status());

        RenderPage().FindAll("[data-testid=previous-library]").ShouldBeEmpty();
    }

    [Fact]
    public void WithoutArchiveName_TheDateStands()
    {
        LibraryCare.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(LibraryCareData.Status(previous: LibraryCareData.Previous() with { RestoredArchive = null }));

        Id(RenderPage(), "previous-library").TextContent.ShouldContain("Kept aside on 17 Nov 2026, 10:15: 5.0 GB");
    }

    [Fact]
    public async Task Undo_StartsTheUndoJob_AndFollowsIt()
    {
        LibraryCare.RevertRestoreAsync(Arg.Any<CancellationToken>()).Returns(LibraryCareData.Job(LibraryJobKindDto.UndoRestore, LibraryJobStatusDto.Queued, null, 0));
        var page = RenderPage();

        await Id(page, "previous-library-undo").ClickAsync(new());

        Id(page, "library-job").GetAttribute("data-kind").ShouldBe("undoRestore");
        page.FindAll("[data-testid=previous-library-error]").ShouldBeEmpty();
    }

    [Fact]
    public async Task Undo_Refused_ShowsTheReason()
    {
        LibraryCare.RevertRestoreAsync(Arg.Any<CancellationToken>()).ThrowsAsync(Refused(HttpStatusCode.Conflict, "jobs-active", "Wait for 1 job to finish."));
        var page = RenderPage();

        await Id(page, "previous-library-undo").ClickAsync(new());

        var error = Id(page, "previous-library-error");
        (error.TextContent, error.GetAttribute("data-code")).ShouldBe(("Wait for 1 job to finish.", "jobs-active"));
    }

    [Fact]
    public void WhileAJobRuns_UndoAndDiscardWait()
    {
        LibraryCare.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(LibraryCareData.Status(LibraryCareData.Job(), previous: LibraryCareData.Previous()));

        var page = RenderPage();

        Id(page, "previous-library-undo").HasAttribute("disabled").ShouldBeTrue();
        Id(page, "previous-library-discard").HasAttribute("disabled").ShouldBeTrue();
        Id(page, "previous-library").TextContent.ShouldContain("wait until the running library job has finished");
    }

    [Fact]
    public async Task Discard_NeedsTheTypedWord_ThenDeletesAndRefreshes()
    {
        var page = RenderPage();

        await Id(page, "previous-library-discard").ClickAsync(new());
        var dialog = page.Find("[role=dialog]");
        dialog.GetAttribute("aria-labelledby").ShouldBe("previous-library-discard-title");
        dialog.TextContent.ShouldContain("deleted for good");
        Id(page, "previous-library-discard-confirm").HasAttribute("disabled").ShouldBeTrue();
        await Id(page, "previous-library-discard-input").InputAsync(new() { Value = "discard" });
        Id(page, "previous-library-discard-confirm").HasAttribute("disabled").ShouldBeTrue();
        await Id(page, "previous-library-discard-input").InputAsync(new() { Value = "DISCARD" });
        LibraryCare.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(LibraryCareData.Status());
        await Id(page, "previous-library-discard-confirm").ClickAsync(new());

        await LibraryCare.Received(1).DiscardPreviousLibraryAsync("DISCARD", Arg.Any<CancellationToken>());
        page.FindAll("[data-testid=previous-library], [role=dialog]").ShouldBeEmpty();
        await LibraryCare.Received(2).ListBackupsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Discard_Refused_StaysOpenWithTheReason()
    {
        LibraryCare.DiscardPreviousLibraryAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(Refused(HttpStatusCode.Conflict, "backup-running", "A backup is running; try again when it has finished."));
        var page = RenderPage();
        await Id(page, "previous-library-discard").ClickAsync(new());
        await Id(page, "previous-library-discard-input").InputAsync(new() { Value = "DISCARD" });

        await Id(page, "previous-library-discard-confirm").ClickAsync(new());

        Id(page, "previous-library-discard-error").GetAttribute("data-code").ShouldBe("backup-running");
        Id(page, "previous-library-discard-confirm").ShouldNotBeNull();
    }

    [Fact]
    public async Task Discard_CancelAndEscape_KeepIt()
    {
        var page = RenderPage();

        await Id(page, "previous-library-discard").ClickAsync(new());
        await Id(page, "previous-library-discard-cancel").ClickAsync(new());
        page.FindAll("[role=dialog]").ShouldBeEmpty();
        await Id(page, "previous-library-discard").ClickAsync(new());
        await page.Find("[role=dialog]").KeyDownAsync(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Escape" });
        page.FindAll("[role=dialog]").ShouldBeEmpty();

        await LibraryCare.DidNotReceive().DiscardPreviousLibraryAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RestoreButton_OpensTheDialog_AndAStartedRestoreIsFollowed()
    {
        LibraryCare.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(LibraryCareData.Status(empty: true));
        LibraryCare.RestoreBackupAsync(Row, Arg.Any<RestoreOptionsDto>(), Arg.Any<CancellationToken>())
            .Returns(LibraryCareData.Job(LibraryJobKindDto.Restore, LibraryJobStatusDto.Running, LibraryJobStageDto.Validate, 5));
        var page = RenderPage();

        await Id(page, $"backup-restore-{Row}").ClickAsync(new());
        Id(page, "restore-dialog").ShouldNotBeNull();
        await Id(page, "restore-start").ClickAsync(new());

        page.FindAll("[data-testid=restore-dialog]").ShouldBeEmpty();
        Id(page, "library-job").GetAttribute("data-stage").ShouldBe("validate");
    }

    [Fact]
    public async Task RestoreDialog_Cancel_ClosesIt()
    {
        var page = RenderPage();

        await Id(page, $"backup-restore-{Row}").ClickAsync(new());
        await Id(page, "restore-dialog-cancel").ClickAsync(new());

        page.FindAll("[data-testid=restore-dialog]").ShouldBeEmpty();
    }

    [Fact]
    public void RestoreReport_ListsTheCounts_TheMigrationAndThePreviousLibrary()
    {
        var report = new RestoreReportDto
        {
            Mode = RestoreReportModeDto.Replace,
            Restored = [new CategoryCountDto { Category = BackupCategoryDto.Stems, Items = 3, Files = 24, Bytes = 800 * StorageData.MiB }],
            Skipped = [new SkippedItemDto { Category = "voiceProfiles", Id = "p1", Name = "Old voice", Reason = SkippedItemReasonDto.ConsentInvalid }],
            MigratedFrom = "20261001000000_Lyrics",
            Warnings = ["The stored Claude key must be entered again"],
            PreviousLibraryKept = true,
        };
        LibraryCare.ListJobsAsync(1, Arg.Any<CancellationToken>()).Returns([LibraryCareData.Completed(LibraryJobKindDto.Restore, report)]);

        var page = RenderPage();

        var text = Id(page, "library-job-report").TextContent;
        text.ShouldContain("The backup was restored. It was updated from an older StemStudio (20261001000000_Lyrics). Your previous library is kept below until you discard it.");
        text.ShouldContain("Stems: 24 files, 800 MB");
        var warnings = Id(page, "library-job-warnings").TextContent;
        warnings.ShouldContain("The stored Claude key must be entered again");
        warnings.ShouldContain("Voice profile \"Old voice\": its consent record is not valid");
    }

    [Fact]
    public void UndoReport_SaysThePreviousLibraryIsBack()
    {
        var report = new RestoreReportDto
        {
            Mode = RestoreReportModeDto.Undo,
            Restored = [],
            Skipped = [],
            MigratedFrom = null,
            Warnings = [],
            PreviousLibraryKept = true,
        };
        LibraryCare.ListJobsAsync(1, Arg.Any<CancellationToken>()).Returns([LibraryCareData.Completed(LibraryJobKindDto.UndoRestore, report)]);

        var page = RenderPage();

        Id(page, "library-job-report").TextContent.ShouldContain("The previous library is back.");
        page.FindAll("[data-testid=library-job-warnings]").ShouldBeEmpty();
    }
}
