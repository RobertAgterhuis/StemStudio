using Bunit;
using StemStudio.Contracts.LibraryCare;
using StemStudio.Web.Components.Care;
using StemStudio.Web.Experience;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Care;

/// <summary>The library job panel (013 T039, FR-006, FR-032; ui-test-ids 1.12.0 library-job-*).</summary>
public sealed class LibraryJobPanelTests : PageTestContext
{
    private IRenderedComponent<LibraryJobPanel> RenderPanel(LibraryJobDto job) => Render<LibraryJobPanel>(p => p.Add(c => c.Job, job));

    [Fact]
    public void RunningJob_ShowsProgressStageItemEtaAndCancel()
    {
        var panel = RenderPanel(LibraryCareData.Job());

        var root = panel.Find("[data-testid=library-job]");
        (root.GetAttribute("data-kind"), root.GetAttribute("data-status"), root.GetAttribute("data-stage")).ShouldBe(("backup", "running", "files"));
        root.GetAttribute("data-job-id").ShouldBe(LibraryCareData.JobId.ToString("D"));
        panel.Find("#library-job-title").TextContent.ShouldBe("Backup running");
        var bar = panel.Find("[data-testid=library-job-progress]");
        (bar.GetAttribute("role"), bar.GetAttribute("aria-valuenow"), bar.GetAttribute("aria-valuetext"), bar.GetAttribute("aria-valuemin"), bar.GetAttribute("aria-valuemax"))
            .ShouldBe(("progressbar", "42", "42 % - Writing files", "0", "100"));
        bar.GetAttribute("aria-labelledby").ShouldBe("library-job-title");
        panel.Find("[data-testid=library-job-stage]").TextContent.ShouldBe("Writing files");
        panel.Find("[data-testid=library-job-item]").TextContent.ShouldBe("Song title - vocals.flac");
        panel.Find("[data-testid=library-job-eta]").TextContent.ShouldBe("about 3 minutes left");
        panel.Find("[data-testid=library-job-bytes]").TextContent.ShouldBe("900 MB of 2.0 GB");
        panel.Find("[data-testid=library-job-cancel]").ShouldNotBeNull();
        panel.FindAll("[data-testid=library-job-report], [data-testid=library-job-error], [data-testid=library-job-warnings]").ShouldBeEmpty();
    }

    [Fact]
    public void LiveRegion_AnnouncesTitleAndStage_Politely()
    {
        var region = RenderPanel(LibraryCareData.Job()).Find("[aria-live=polite]");

        (region.GetAttribute("role"), region.TextContent).ShouldBe(("status", "Backup running. Writing files."));
    }

    [Theory]
    [InlineData(ExperienceLevel.Beginner, 0)]
    [InlineData(ExperienceLevel.Advanced, 1)]
    public void Bytes_AreShownFromAdvanced(ExperienceLevel level, int count) =>
        RenderAtLevel<LibraryJobPanel>(level, p => p.Add(c => c.Job, LibraryCareData.Job())).FindAll("[data-testid=library-job-bytes]").Count.ShouldBe(count);

    [Fact]
    public void ServerStageText_WinsOverTheBuiltInText()
    {
        var panel = RenderPanel(LibraryCareData.Job() with { StageText = "Copying stems of Song" });

        panel.Find("[data-testid=library-job-stage]").TextContent.ShouldBe("Copying stems of Song");
    }

    [Theory]
    [InlineData(LibraryJobStageDto.Snapshot, "Copying the database")]
    [InlineData(LibraryJobStageDto.Scrub, "Preparing the database copy")]
    [InlineData(LibraryJobStageDto.Manifest, "Writing the contents list")]
    [InlineData(LibraryJobStageDto.Check, "Checking the archive")]
    [InlineData(LibraryJobStageDto.Validate, "Checking the archive")]
    [InlineData(LibraryJobStageDto.Extract, "Unpacking files")]
    [InlineData(LibraryJobStageDto.Database, "Checking the database")]
    [InlineData(LibraryJobStageDto.Switch, "Switching to the restored library")]
    [InlineData(LibraryJobStageDto.Reconcile, "Finishing")]
    public void EveryStage_HasAText(LibraryJobStageDto stage, string text) =>
        RenderPanel(LibraryCareData.Job(stage: stage)).Find("[data-testid=library-job-stage]").TextContent.ShouldBe(text);

    [Fact]
    public void NotCancellable_HasNoCancel_AndNoEtaOrItemWhenUnknown()
    {
        var panel = RenderPanel(LibraryCareData.Job() with { Cancellable = false, EtaSeconds = null, CurrentItem = null, BytesTotal = 0 });

        panel.FindAll("[data-testid=library-job-cancel], [data-testid=library-job-eta], [data-testid=library-job-item], [data-testid=library-job-bytes]").ShouldBeEmpty();
    }

    [Fact]
    public void CompletedBackup_ShowsTheReportAndWarnings_At100()
    {
        var panel = RenderPanel(LibraryCareData.CompletedBackup());

        panel.Find("#library-job-title").TextContent.ShouldBe("Last backup: completed");
        panel.Find("[data-testid=library-job-progress]").GetAttribute("aria-valuenow").ShouldBe("100");
        panel.Find("[data-testid=library-job-stage]").TextContent.ShouldBe("Completed");
        var report = panel.Find("[data-testid=library-job-report]").TextContent;
        report.ShouldContain($"Backup {LibraryCareData.Archive} written: 412 files, 2.0 GB.");
        report.ShouldContain("Stems: 24 files, 800 MB");
        panel.Find("[data-testid=library-job-warnings]").TextContent.ShouldContain("Artwork of \"Song\" was missing");
        panel.FindAll("[data-testid=library-job-cancel], [data-testid=library-job-eta], [data-testid=library-job-item]").ShouldBeEmpty();
    }

    [Fact]
    public void FailedJob_ShowsTheErrorWithItsCode()
    {
        var panel = RenderPanel(LibraryCareData.Job(status: LibraryJobStatusDto.Failed) with
        {
            Cancellable = false,
            Error = new LibraryJobErrorDto { Code = LibraryJobErrorCodeDto.InsufficientDiskSpace, Message = "Not enough free space" },
        });

        var error = panel.Find("[data-testid=library-job-error]");
        (error.TextContent, error.GetAttribute("data-code"), error.GetAttribute("role")).ShouldBe(("Not enough free space", "insufficient-disk-space", "alert"));
        panel.Find("#library-job-title").TextContent.ShouldBe("Last backup: failed");
        panel.FindAll("[data-testid=library-job-report]").ShouldBeEmpty();
    }

    [Fact]
    public void Verify_Ok_AndProblem_HaveTheirReports()
    {
        var ok = RenderPanel(LibraryCareData.Completed(LibraryJobKindDto.Verify, new VerifyReportDto
        {
            Ok = true,
            EntriesChecked = 412,
            BytesChecked = 2L * 1024 * 1024 * 1024,
            Problem = null,
        }));
        ok.Find("[data-testid=library-job-report]").TextContent.ShouldContain("The archive is complete and readable: 412 files checked, 2.0 GB.");
        ok.Find("#library-job-title").TextContent.ShouldBe("Last check: completed");

        var bad = RenderPanel(LibraryCareData.Completed(LibraryJobKindDto.Verify, new VerifyReportDto
        {
            Ok = false,
            EntriesChecked = 3,
            BytesChecked = 10,
            Problem = new LibraryJobErrorDto { Code = LibraryJobErrorCodeDto.ArchiveCorrupt, Message = "files/x.flac: hash mismatch" },
        }));
        bad.Find("[data-testid=library-job-report]").TextContent.ShouldContain("The check found a problem: files/x.flac: hash mismatch.");

        var unknown = RenderPanel(LibraryCareData.Completed(LibraryJobKindDto.Verify, new VerifyReportDto
        {
            Ok = false,
            EntriesChecked = 3,
            BytesChecked = 10,
            Problem = null,
        }));
        unknown.Find("[data-testid=library-job-report]").TextContent.ShouldContain("the archive is damaged");
    }

    [Fact]
    public void CompletedWithoutReport_SaysCompleted()
    {
        RenderPanel(LibraryCareData.Job(LibraryJobKindDto.UndoRestore, LibraryJobStatusDto.Completed, null, 100))
            .Find("[data-testid=library-job-report]").TextContent.ShouldContain("Undo restore completed.");
    }

    [Fact]
    public void QueuedJob_WaitsToStart_AtZero()
    {
        var panel = RenderPanel(LibraryCareData.Job(status: LibraryJobStatusDto.Queued, stage: null, progress: 0));

        panel.Find("[data-testid=library-job-stage]").TextContent.ShouldBe("Waiting to start");
        panel.Find("[data-testid=library-job-progress]").GetAttribute("aria-valuetext").ShouldBe("0 % - Waiting to start");
        panel.Find("[data-testid=library-job]").HasAttribute("data-stage").ShouldBeFalse();
    }

    [Fact]
    public void CancelError_IsShown_WhenTheHostPassesOne()
    {
        var panel = Render<LibraryJobPanel>(p => p.Add(c => c.Job, LibraryCareData.Job()).Add(c => c.CancelError, "No").Add(c => c.CancelErrorCode, "not-cancellable"));

        panel.Find("[data-testid=library-job-error]").GetAttribute("data-code").ShouldBe("not-cancellable");
    }
}
