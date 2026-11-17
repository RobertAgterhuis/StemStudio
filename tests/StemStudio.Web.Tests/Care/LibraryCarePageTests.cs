using System.Net;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Contracts.LibraryCare;
using StemStudio.Web.Components.Care;
using StemStudio.Web.Experience;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Care;

/// <summary>
/// Library care page and Backups tab (013 T039, US1, FR-001-FR-009, FR-030-FR-033; ui-test-ids 1.12.0): tabs, folder
/// state, New backup, the archive list with its actions, details and the delete confirmation.
/// </summary>
public sealed class LibraryCarePageTests : PageTestContext
{
    private const string Row = LibraryCareData.Archive;

    private IRenderedComponent<LibraryCarePage> RenderPage() => Render<LibraryCarePage>();

    private static AngleSharp.Dom.IElement Id(IRenderedComponent<LibraryCarePage> page, string id) => page.Find($"[data-testid='{id}']");

    [Fact]
    public void Page_HasTitleHelpAndGate_AndTheBackupsTabSelected()
    {
        var page = RenderPage();

        page.Find("h1").TextContent.ShouldBe("Library care");
        var root = Id(page, "care-page");
        (root.GetAttribute("data-gate"), root.GetAttribute("data-help-id")).ShouldBe(("open", "care-page"));
        page.Find("[role=tablist]").GetAttribute("aria-label").ShouldBe("Library care");
        Id(page, "care-tab-backups").GetAttribute("aria-selected").ShouldBe("true");
        Id(page, "care-tab-storage").GetAttribute("aria-selected").ShouldBe("false");
        (Id(page, "care-tab-backups").GetAttribute("tabindex"), Id(page, "care-tab-storage").GetAttribute("tabindex")).ShouldBe(("0", "-1"));
        page.Find("[role=tabpanel]").GetAttribute("aria-labelledby").ShouldBe("care-tab-backups");
        Id(page, $"backup-row-{Row}").ShouldNotBeNull();
    }

    [Fact]
    public void TabQuery_Storage_SelectsTheStorageTab()
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo("care?tab=storage");

        var page = RenderPage();

        Id(page, "care-tab-storage").GetAttribute("aria-selected").ShouldBe("true");
        page.FindAll("[data-testid=backup-new]").ShouldBeEmpty();
    }

    [Fact]
    public async Task Tabs_SwitchByClick_AndByArrowKeys_AndKeepTheQueryInTheAddress()
    {
        var page = RenderPage();

        await Id(page, "care-tab-storage").ClickAsync(new());
        Id(page, "care-tab-storage").GetAttribute("aria-selected").ShouldBe("true");
        NavigatedTo.ShouldEndWith("care?tab=storage");

        await page.Find("[role=tablist]").KeyDownAsync(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "ArrowLeft" });
        Id(page, "care-tab-backups").GetAttribute("aria-selected").ShouldBe("true");
        NavigatedTo.ShouldEndWith("/care");

        await page.Find("[role=tablist]").KeyDownAsync(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "End" });
        Id(page, "care-tab-storage").GetAttribute("aria-selected").ShouldBe("true");
        await page.Find("[role=tablist]").KeyDownAsync(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Home" });
        Id(page, "care-tab-backups").GetAttribute("aria-selected").ShouldBe("true");
        await page.Find("[role=tablist]").KeyDownAsync(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "a" });
        Id(page, "care-tab-backups").GetAttribute("aria-selected").ShouldBe("true");
        await Id(page, "care-tab-backups").ClickAsync(new());
        Id(page, "care-tab-backups").GetAttribute("aria-selected").ShouldBe("true");
    }

    [Fact]
    public void Gate_FollowsTheStatus()
    {
        LibraryCare.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(LibraryCareData.Status(LibraryCareData.Job(), GateModeDto.BackupRunning));

        Id(RenderPage(), "care-page").GetAttribute("data-gate").ShouldBe("backupRunning");
    }

    [Fact]
    public void FolderState_ShowsHintAndFreeSpace_WithoutErrorWhenWritable()
    {
        var page = RenderPage();

        var folder = Id(page, "backup-folder-state");
        (folder.GetAttribute("data-available"), folder.GetAttribute("data-writable")).ShouldBe(("true", "true"));
        folder.TextContent.ShouldContain("./backups");
        folder.TextContent.ShouldContain("120 GB free");
        page.Find("[data-help-id=backup-folder-state]").ShouldNotBeNull();
        page.FindAll("[data-testid=backup-folder-error]").ShouldBeEmpty();
        Id(page, "backup-new").HasAttribute("disabled").ShouldBeFalse();
    }

    [Theory]
    [InlineData(false, true, "not available: missing")]
    [InlineData(true, false, "read-only: missing")]
    public void FolderProblem_ShowsReasonAndFix_AndDisablesNewBackup(bool available, bool writable, string text)
    {
        LibraryCare.ListBackupsAsync(Arg.Any<CancellationToken>()).Returns(new BackupListDto { Folder = LibraryCareData.Folder(available, writable, null, "missing"), Archives = [] });

        var page = RenderPage();

        var error = Id(page, "backup-folder-error");
        error.TextContent.ShouldContain(text);
        error.TextContent.ShouldContain("writable");
        (error.GetAttribute("role"), error.GetAttribute("data-help-id")).ShouldBe(("alert", "backup-folder-error"));
        Id(page, "backup-folder-state").TextContent.ShouldNotContain("free");
        Id(page, "backup-new").HasAttribute("disabled").ShouldBeTrue();
        Id(page, "backup-new").GetAttribute("aria-describedby").ShouldBe("backup-new-reason");
        page.Find("#backup-new-reason").TextContent.ShouldContain("not available");
    }

    [Fact]
    public void FolderState_ComesFromTheStatus_WhenTheListFails()
    {
        LibraryCare.ListBackupsAsync(Arg.Any<CancellationToken>()).ThrowsAsync(Problem(HttpStatusCode.ServiceUnavailable, "Down"));

        var page = RenderPage();

        Id(page, "backup-folder-state").GetAttribute("data-available").ShouldBe("true");
        Id(page, "backup-list-error").TextContent.ShouldBe("Down");
        page.FindAll("[data-testid=backup-list]").ShouldBeEmpty();
    }

    [Fact]
    public void NewBackup_IsDisabledWithTheReason_WhileAJobRuns()
    {
        LibraryCare.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(LibraryCareData.Status(LibraryCareData.Job(LibraryJobKindDto.Verify)));

        var page = RenderPage();

        Id(page, "backup-new").HasAttribute("disabled").ShouldBeTrue();
        page.Find("#backup-new-reason").TextContent.ShouldContain("library job is running");
        Id(page, "library-job").GetAttribute("data-kind").ShouldBe("verify");
    }

    [Fact]
    public void List_ShowsEveryArchive_WithStateStatusAndActions()
    {
        LibraryCare.ListBackupsAsync(Arg.Any<CancellationToken>()).Returns(LibraryCareData.List(
            LibraryCareData.Summary(),
            LibraryCareData.Summary("older.zip", LibraryCareData.Compatible(migration: true)),
            LibraryCareData.Summary("newer.zip", LibraryCareData.Compatible(false, reason: "made by StemStudio 2.0")),
            LibraryCareData.Summary("photos.zip", recognised: false)));

        var page = RenderPage();

        page.FindAll("[data-testid=backup-list] tbody tr").Select(r => r.GetAttribute("data-state"))
            .ShouldBe(["restorable", "migration", "incompatible", "unrecognised"]);
        Id(page, $"backup-row-status-{Row}").TextContent.ShouldBe("Ready to restore");
        Id(page, "backup-row-status-older.zip").TextContent.ShouldContain("older StemStudio");
        Id(page, "backup-row-status-newer.zip").TextContent.ShouldBe("Not compatible: made by StemStudio 2.0");
        Id(page, "backup-row-status-photos.zip").TextContent.ShouldBe("Not a StemStudio backup: no manifest.json");
        Id(page, $"backup-row-status-{Row}").GetAttribute("data-help-id").ShouldBe($"backup-row-status-{Row}");
        Id(page, $"backup-row-{Row}").TextContent.ShouldContain("17 Nov 2026, 10:15");
        Id(page, $"backup-row-{Row}").TextContent.ShouldContain("3 tracks, 1 mix, 2 library items, 1 generation, 1 voice profile");
        Id(page, $"backup-row-{Row}").TextContent.ShouldContain("2.0 GB");
        Id(page, "backup-row-photos.zip").TextContent.ShouldContain("Changed 17 Nov 2026");
        Id(page, "backup-restore-newer.zip").HasAttribute("disabled").ShouldBeTrue();
        Id(page, "backup-restore-photos.zip").HasAttribute("disabled").ShouldBeTrue();
        Id(page, "backup-verify-photos.zip").HasAttribute("disabled").ShouldBeTrue();
        Id(page, $"backup-restore-{Row}").HasAttribute("disabled").ShouldBeFalse();
        Id(page, "backup-restore-older.zip").GetAttribute("aria-describedby").ShouldBe("backup-row-status-older.zip");
    }

    [Fact]
    public void Download_LinksStraightToTheApi()
    {
        var link = Id(RenderPage(), $"backup-download-{Row}");

        (link.TagName, link.GetAttribute("href"), link.GetAttribute("download"))
            .ShouldBe(("A", $"http://localhost:8081/api/backups/{Row}/file", Row));
    }

    [Fact]
    public void EmptyList_ShowsTheFolderHint()
    {
        LibraryCare.ListBackupsAsync(Arg.Any<CancellationToken>()).Returns(LibraryCareData.List());

        var page = RenderPage();

        Id(page, "backup-empty").TextContent.ShouldContain("copy an archive into ./backups to restore it");
        page.FindAll("[data-testid=backup-list]").ShouldBeEmpty();
    }

    [Fact]
    public void WhileAJobRuns_RestoreAndCheckWait()
    {
        LibraryCare.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(LibraryCareData.Status(LibraryCareData.Job()));

        var page = RenderPage();

        Id(page, $"backup-restore-{Row}").HasAttribute("disabled").ShouldBeTrue();
        Id(page, $"backup-verify-{Row}").HasAttribute("disabled").ShouldBeTrue();
        Id(page, $"backup-delete-{Row}").HasAttribute("disabled").ShouldBeFalse();
        page.Markup.ShouldContain("wait until the running library job has finished");
    }

    [Theory]
    [InlineData(ExperienceLevel.Beginner, false)]
    [InlineData(ExperienceLevel.Advanced, true)]
    [InlineData(ExperienceLevel.Expert, true)]
    public void Verify_IsShownFromAdvanced(ExperienceLevel level, bool shown)
    {
        var page = RenderAtLevel<LibraryCarePage>(level, _ => { });

        page.FindAll($"[data-testid='backup-verify-{Row}']").Count.ShouldBe(shown ? 1 : 0);
        page.FindAll($"[data-testid='backup-restore-{Row}']").Count.ShouldBe(1);
        page.FindAll($"[data-testid='backup-download-{Row}']").Count.ShouldBe(1);
    }

    [Fact]
    public async Task Verify_StartsAJob_AndFollowsIt()
    {
        LibraryCare.VerifyBackupAsync(Row, Arg.Any<CancellationToken>()).Returns(LibraryCareData.Job(LibraryJobKindDto.Verify, LibraryJobStatusDto.Queued, null, 0));
        var page = RenderPage();

        await Id(page, $"backup-verify-{Row}").ClickAsync(new());

        Id(page, "library-job").GetAttribute("data-kind").ShouldBe("verify");
        Id(page, "library-job").GetAttribute("data-status").ShouldBe("queued");
        await LibraryCare.Received(2).GetStatusAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Verify_Refused_ShowsTheReadableReasonWithItsCode()
    {
        LibraryCare.VerifyBackupAsync(Row, Arg.Any<CancellationToken>()).ThrowsAsync(LibraryJobRunning());
        var page = RenderPage();

        await Id(page, $"backup-verify-{Row}").ClickAsync(new());

        var error = Id(page, "backup-error");
        (error.TextContent, error.GetAttribute("data-code")).ShouldBe(("Another library job is running.", "library-job-running"));
    }

    [Fact]
    public async Task Delete_AsksForConfirmation_ThenDeletesAndReloadsTheList()
    {
        var page = RenderPage();
        await Id(page, $"backup-details-{Row}").ClickAsync(new());

        await Id(page, $"backup-delete-{Row}").ClickAsync(new());
        page.Find("[role=dialog]").TextContent.ShouldContain($"Delete \"{Row}\" from the backups folder?");
        LibraryCare.ListBackupsAsync(Arg.Any<CancellationToken>()).Returns(LibraryCareData.List());
        await Id(page, "backup-delete-confirm").ClickAsync(new());

        await LibraryCare.Received(1).DeleteBackupAsync(Row, Arg.Any<CancellationToken>());
        page.FindAll("[data-testid=backup-delete-confirm]").ShouldBeEmpty();
        page.FindAll("[data-testid=backup-details]").ShouldBeEmpty();
        Id(page, "backup-empty").ShouldNotBeNull();
    }

    [Fact]
    public async Task Delete_Cancel_KeepsTheArchive()
    {
        var page = RenderPage();

        await Id(page, $"backup-delete-{Row}").ClickAsync(new());
        await Id(page, "backup-delete-cancel").ClickAsync(new());

        await LibraryCare.DidNotReceive().DeleteBackupAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        page.FindAll("[role=dialog]").ShouldBeEmpty();
    }

    [Fact]
    public async Task Delete_RefusedDuringABackup_ShowsThe409Text()
    {
        LibraryCare.DeleteBackupAsync(Row, Arg.Any<CancellationToken>()).ThrowsAsync(new Web.Api.ApiException(
            new Web.Api.ApiError(HttpStatusCode.Conflict, "Archive in use", "This archive is being written or read by a library job.", new Dictionary<string, string[]>())
            {
                Type = "https://stemstudio.local/problems/archive-in-use",
            }));
        var page = RenderPage();

        await Id(page, $"backup-delete-{Row}").ClickAsync(new());
        await Id(page, "backup-delete-confirm").ClickAsync(new());

        var error = Id(page, "backup-error");
        (error.TextContent, error.GetAttribute("data-code")).ShouldBe(("This archive is being written or read by a library job.", "archive-in-use"));
        Id(page, $"backup-row-{Row}").ShouldNotBeNull();
    }

    [Fact]
    public async Task Details_ShowContentsCountsAndConsentProofs_AndCloseAgain()
    {
        var page = RenderPage();

        await Id(page, $"backup-details-{Row}").ClickAsync(new());

        var details = Id(page, "backup-details");
        details.GetAttribute("aria-labelledby").ShouldBe("backup-details-title");
        Id(page, "backup-details-contents").TextContent.ShouldContain("Database (tracks, mixes, settings), Stems, Voice profiles");
        Id(page, "backup-details-contents").TextContent.ShouldContain("Detail stems");
        Id(page, "backup-details-counts").TextContent.ShouldContain("3 tracks");
        var proof = Id(page, $"backup-details-voice-{LibraryCareData.ProfileId}");
        proof.GetAttribute("data-valid").ShouldBe("true");
        proof.GetAttribute("data-help-id").ShouldBe($"backup-details-voice-{LibraryCareData.ProfileId}");
        proof.TextContent.ShouldBe("My voice: agreed by Sam Example on 14 Nov 2026 (statement consent.v1)");

        await Id(page, "backup-details-close").ClickAsync(new());
        page.FindAll("[data-testid=backup-details]").ShouldBeEmpty();
    }

    [Fact]
    public async Task Details_InvalidProof_SaysItWillNotBeRestored()
    {
        LibraryCare.GetBackupAsync(Row, Arg.Any<CancellationToken>()).Returns(LibraryCareData.Details(validProof: false));
        var page = RenderPage();

        await Id(page, $"backup-details-{Row}").ClickAsync(new());

        var proof = Id(page, $"backup-details-voice-{LibraryCareData.ProfileId}");
        proof.GetAttribute("data-valid").ShouldBe("false");
        proof.TextContent.ShouldContain("not valid: statement hash mismatch; it will not be restored");
    }

    [Fact]
    public async Task Details_Failure_ShowsTheError()
    {
        LibraryCare.GetBackupAsync(Row, Arg.Any<CancellationToken>()).ThrowsAsync(Problem(HttpStatusCode.UnprocessableEntity, "The archive cannot be read."));
        var page = RenderPage();

        await Id(page, $"backup-details-{Row}").ClickAsync(new());

        Id(page, "backup-details-error").TextContent.ShouldBe("The archive cannot be read.");
    }

    [Theory]
    [InlineData(ExperienceLevel.Advanced, false)]
    [InlineData(ExperienceLevel.Expert, true)]
    public async Task Details_Manifest_IsShownAtExpert(ExperienceLevel level, bool shown)
    {
        var page = RenderAtLevel<LibraryCarePage>(level, _ => { });

        await page.Find($"[data-testid='backup-details-{Row}']").ClickAsync(new());

        page.FindAll("[data-testid=backup-details-manifest]").Count.ShouldBe(shown ? 1 : 0);
        if (shown)
        {
            var manifest = page.Find("[data-testid=backup-details-manifest]").TextContent;
            foreach (var text in new[] { "1.0.0", "1.13.0", "API 1.12.0", "20261116000000_LibraryCare", "412", "3.0 GB", "1 file was missing" })
            {
                manifest.ShouldContain(text);
            }
        }
    }

    [Fact]
    public void LastJob_IsShown_WhenNoJobRuns()
    {
        LibraryCare.ListJobsAsync(1, Arg.Any<CancellationToken>()).Returns([LibraryCareData.CompletedBackup()]);

        var page = RenderPage();

        Id(page, "library-job").GetAttribute("data-status").ShouldBe("completed");
        Id(page, "library-job-report").TextContent.ShouldContain($"Backup {LibraryCareData.Archive} written");
        page.Find("#library-job-title").TextContent.ShouldBe("Last backup: completed");
    }

    [Fact]
    public void NoJobEver_ShowsNoPanel()
    {
        RenderPage().FindAll("[data-testid=library-job]").ShouldBeEmpty();
    }

    [Fact]
    public async Task ActiveJob_WhenItEnds_ShowsTheFinalJob_AndReloadsTheList()
    {
        LibraryCare.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(LibraryCareData.Status(LibraryCareData.Job()), LibraryCareData.Status());
        LibraryCare.GetJobAsync(LibraryCareData.JobId, Arg.Any<CancellationToken>()).Returns(LibraryCareData.CompletedBackup());
        var page = RenderPage();
        Id(page, "library-job").GetAttribute("data-status").ShouldBe("running");

        Time.Advance(TimeSpan.FromSeconds(2));

        page.WaitForAssertion(() => Id(page, "library-job").GetAttribute("data-status").ShouldBe("completed"));
        await LibraryCare.Received(2).ListBackupsAsync(Arg.Any<CancellationToken>());
        Id(page, "backup-new").HasAttribute("disabled").ShouldBeFalse();
    }

    [Fact]
    public async Task Cancel_CancelsTheActiveJob()
    {
        LibraryCare.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(LibraryCareData.Status(LibraryCareData.Job()));
        LibraryCare.CancelJobAsync(LibraryCareData.JobId, Arg.Any<CancellationToken>()).Returns(LibraryCareData.Job(status: LibraryJobStatusDto.Cancelled) with { Cancellable = false });
        var page = RenderPage();

        await Id(page, "library-job-cancel").ClickAsync(new());

        await LibraryCare.Received(1).CancelJobAsync(LibraryCareData.JobId, Arg.Any<CancellationToken>());
        page.FindAll("[data-testid=library-job-error]").ShouldBeEmpty();
    }

    [Fact]
    public async Task Cancel_Refused_ShowsTheReason()
    {
        LibraryCare.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(LibraryCareData.Status(LibraryCareData.Job(LibraryJobKindDto.Restore, stage: LibraryJobStageDto.Extract)));
        LibraryCare.CancelJobAsync(LibraryCareData.JobId, Arg.Any<CancellationToken>()).ThrowsAsync(new Web.Api.ApiException(
            new Web.Api.ApiError(HttpStatusCode.Conflict, "Not cancellable", "The restore is switching the library.", new Dictionary<string, string[]>())
            {
                Type = "https://stemstudio.local/problems/not-cancellable",
            }));
        var page = RenderPage();

        await Id(page, "library-job-cancel").ClickAsync(new());

        var error = Id(page, "library-job-error");
        (error.TextContent, error.GetAttribute("data-code")).ShouldBe(("The restore is switching the library.", "not-cancellable"));
    }

    [Fact]
    public async Task NewBackup_OpensTheDialog_AndAStartedBackupIsFollowed()
    {
        LibraryCare.CreateBackupAsync(Arg.Any<BackupOptionsDto>(), Arg.Any<CancellationToken>()).Returns(LibraryCareData.Job(status: LibraryJobStatusDto.Queued, stage: null, progress: 0));
        var page = RenderPage();

        await Id(page, "backup-new").ClickAsync(new());
        Id(page, "backup-dialog").ShouldNotBeNull();
        await Id(page, "backup-start").ClickAsync(new());

        page.FindAll("[data-testid=backup-dialog]").ShouldBeEmpty();
        Id(page, "library-job").GetAttribute("data-status").ShouldBe("queued");
        Id(page, "library-job-stage").TextContent.ShouldBe("Waiting to start");
    }

    [Fact]
    public async Task BackupDialog_Cancel_ClosesIt()
    {
        var page = RenderPage();

        await Id(page, "backup-new").ClickAsync(new());
        await Id(page, "backup-dialog-cancel").ClickAsync(new());

        page.FindAll("[data-testid=backup-dialog]").ShouldBeEmpty();
    }

    [Fact]
    public void Page_IsDisposed_WithoutFurtherRenders()
    {
        var page = RenderPage();

        page.Instance.Dispose();
        Time.Advance(TimeSpan.FromSeconds(10));

        Id(page, "care-page").ShouldNotBeNull();
    }

    private static Web.Api.ApiException LibraryJobRunning() => new(
        new Web.Api.ApiError(HttpStatusCode.Conflict, "Library job running", "Another library job is running.", new Dictionary<string, string[]>())
        {
            Type = "https://stemstudio.local/problems/library-job-running",
        });
}
