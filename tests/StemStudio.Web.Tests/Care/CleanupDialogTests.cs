using System.Net;
using Bunit;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Contracts.LibraryCare;
using StemStudio.Web.Api;
using StemStudio.Web.Components.Care;
using StemStudio.Web.LibraryCare;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Care;

/// <summary>
/// The clean-up dialog and the clean-up controls of the Storage tab (013 T060, US4, FR-022-FR-024, FR-032;
/// ui-test-ids 1.12.0 cleanup-*).
/// </summary>
public sealed class CleanupDialogTests : PageTestContext
{
    private bool? _closed;

    public CleanupDialogTests()
    {
        LibraryCare.GetStorageOverviewAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(StorageData.Overview());
        LibraryCare.PreviewCleanupAsync(Arg.Any<CleanupRequestDto>(), Arg.Any<CancellationToken>()).Returns(Plan());
        LibraryCare.ExecuteCleanupAsync(StorageData.PlanId, Arg.Any<CancellationToken>()).Returns(new CleanupResultDto
        {
            PlanId = StorageData.PlanId,
            FreedBytes = 600 * StorageData.MiB,
            Removed = [new CleanupRemovedDto { Kind = CleanupKindDto.DetailStems, TrackId = StorageData.TrackA, Files = 12, Bytes = 600 * StorageData.MiB }],
            Failed = [],
        });
    }

    private static CleanupPlanDto Plan(bool skipped = true) => new()
    {
        PlanId = StorageData.PlanId,
        CreatedAt = StorageData.Measured,
        ExpiresAt = StorageData.Measured.AddMinutes(10),
        Items =
        [
            new CleanupItemDto
            {
                Kind = CleanupKindDto.DetailStems, TrackId = StorageData.TrackA, Title = "Big song", Files = 12, Bytes = 600 * StorageData.MiB,
                HowToRegenerate = "Split further on the track page.",
            },
        ],
        Skipped = skipped ? [new CleanupSkipDto { Kind = CleanupKindDto.DetailStems, TrackId = StorageData.TrackB, Title = "Small song", Reason = CleanupSkipReasonDto.DetailActive }] : [],
        TotalBytes = 600 * StorageData.MiB,
    };

    private IRenderedComponent<CleanupDialog> RenderDialog(CleanupRequestDto? request = null) =>
        Render<CleanupDialog>(p => p.Add(d => d.Request, request ?? CleanupText.Request(CleanupKindDto.DetailStems)).Add(d => d.OnClose, removed => _closed = removed));

    private static ApiException Refused(string code, string detail) =>
        new(new ApiError(HttpStatusCode.Conflict, "Refused", detail, new Dictionary<string, string[]>()) { Type = $"https://stemstudio.local/problems/{code}" });

    [Fact]
    public void Preview_ListsItemsSkippedAndTotal_InAModal()
    {
        var dialog = RenderDialog();

        var root = dialog.Find("[data-testid=cleanup-dialog]");
        (root.GetAttribute("role"), root.GetAttribute("aria-modal"), root.GetAttribute("data-help-id")).ShouldBe(("dialog", "true", "cleanup-dialog"));
        dialog.Find("#cleanup-dialog-title").TextContent.ShouldBe("Remove detail stems");
        dialog.Find($"[data-testid=cleanup-plan-item-detail-stems-{StorageData.TrackA}]").TextContent
            .ShouldContain("Big song: 12 files, 600 MB. Split further on the track page.");
        dialog.Find("[data-testid=cleanup-plan-skipped]").TextContent.ShouldContain("Small song: a split is running for this track");
        var total = dialog.Find("[data-testid=cleanup-plan-total]");
        (total.TextContent, total.GetAttribute("data-bytes")).ShouldBe(("Frees 600 MB", (600 * StorageData.MiB).ToString(System.Globalization.CultureInfo.InvariantCulture)));
        dialog.Find("[data-testid=cleanup-confirm]").HasAttribute("disabled").ShouldBeFalse();
        LibraryCare.Received(1).PreviewCleanupAsync(Arg.Is<CleanupRequestDto>(r => r.Actions.Single().Kind == CleanupKindDto.DetailStems && r.Actions.Single().TrackIds == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void WorkerWideItems_UseAll_AndNoSkippedBlockWithoutSkips()
    {
        LibraryCare.PreviewCleanupAsync(Arg.Any<CleanupRequestDto>(), Arg.Any<CancellationToken>()).Returns(Plan(skipped: false) with
        {
            Items = [new CleanupItemDto { Kind = CleanupKindDto.Leftovers, TrackId = null, Title = null, Files = 7, Bytes = 5 * StorageData.MiB, HowToRegenerate = "Not needed." }],
        });

        var dialog = RenderDialog(CleanupText.Request(CleanupKindDto.Leftovers));

        dialog.Find("#cleanup-dialog-title").TextContent.ShouldBe("Remove leftovers");
        dialog.Find("[data-testid=cleanup-plan-item-leftovers-all]").TextContent.ShouldStartWith("Leftovers: 7 files");
        dialog.FindAll("[data-testid=cleanup-plan-skipped]").ShouldBeEmpty();
    }

    [Fact]
    public void EmptyPlan_SaysSo_AndConfirmIsOff()
    {
        LibraryCare.PreviewCleanupAsync(Arg.Any<CleanupRequestDto>(), Arg.Any<CancellationToken>()).Returns(Plan() with
        {
            Items = [],
            TotalBytes = 0,
            Skipped = [new CleanupSkipDto { Kind = CleanupKindDto.WorkerCache, TrackId = null, Title = null, Reason = CleanupSkipReasonDto.WorkerUnavailable }],
        });

        var dialog = RenderDialog(CleanupText.Request(CleanupKindDto.WorkerCache));

        dialog.Find("#cleanup-dialog-title").TextContent.ShouldBe("Clear worker cache");
        dialog.Markup.ShouldContain("There is nothing to remove.");
        dialog.Find("[data-testid=cleanup-plan-skipped]").TextContent.ShouldContain("Worker cache: the worker is not available");
        dialog.Find("[data-testid=cleanup-confirm]").HasAttribute("disabled").ShouldBeTrue();
    }

    [Fact]
    public async Task Confirm_ExecutesThePlan_AndShowsTheFreedSize()
    {
        var dialog = RenderDialog();

        await dialog.Find("[data-testid=cleanup-confirm]").ClickAsync(new());

        await LibraryCare.Received(1).ExecuteCleanupAsync(StorageData.PlanId, Arg.Any<CancellationToken>());
        var result = dialog.Find("[data-testid=cleanup-result]");
        (result.TextContent.Trim(), result.GetAttribute("data-freed-bytes")).ShouldBe(("Freed 600 MB.", (600 * StorageData.MiB).ToString(System.Globalization.CultureInfo.InvariantCulture)));
        dialog.FindAll("[data-testid=cleanup-confirm]").ShouldBeEmpty();
        dialog.Find("[data-testid=cleanup-dialog-cancel]").TextContent.ShouldBe("Close");
        await dialog.Find("[data-testid=cleanup-dialog-cancel]").ClickAsync(new());
        _closed.ShouldBe(true);
    }

    [Fact]
    public async Task Result_ListsWhatFailed()
    {
        LibraryCare.ExecuteCleanupAsync(StorageData.PlanId, Arg.Any<CancellationToken>()).Returns(new CleanupResultDto
        {
            PlanId = StorageData.PlanId,
            FreedBytes = 0,
            Removed = [],
            Failed = [new CleanupSkipDto { Kind = CleanupKindDto.DetailStems, TrackId = StorageData.TrackA, Title = "Big song", Reason = CleanupSkipReasonDto.ExportActive }],
        });
        var dialog = RenderDialog();

        await dialog.Find("[data-testid=cleanup-confirm]").ClickAsync(new());

        dialog.Find("[data-testid=cleanup-result]").TextContent.ShouldContain("Big song: an export uses it");
    }

    [Theory]
    [InlineData("plan-stale", "Your library changed since the preview.")]
    [InlineData("plan-expired", "The preview is older than 10 minutes.")]
    [InlineData("backup-running", "A backup is running; try again when it has finished.")]
    public async Task RefusedPlan_ShowsTheReason_AndPreviewAgainMakesANewPlan(string code, string detail)
    {
        LibraryCare.ExecuteCleanupAsync(StorageData.PlanId, Arg.Any<CancellationToken>()).ThrowsAsync(Refused(code, detail));
        var dialog = RenderDialog();

        await dialog.Find("[data-testid=cleanup-confirm]").ClickAsync(new());

        var error = dialog.Find("[data-testid=cleanup-error]");
        (error.TextContent.Contains(detail, StringComparison.Ordinal), error.GetAttribute("data-code")).ShouldBe((true, code));
        dialog.Find("[data-testid=cleanup-confirm]").HasAttribute("disabled").ShouldBeTrue();

        await dialog.Find("[data-testid=cleanup-repreview]").ClickAsync(new());

        await LibraryCare.Received(2).PreviewCleanupAsync(Arg.Any<CleanupRequestDto>(), Arg.Any<CancellationToken>());
        dialog.FindAll("[data-testid=cleanup-error]").ShouldBeEmpty();
        dialog.Find("[data-testid=cleanup-confirm]").HasAttribute("disabled").ShouldBeFalse();
    }

    [Fact]
    public void PreviewRefused_ShowsTheReason()
    {
        LibraryCare.PreviewCleanupAsync(Arg.Any<CleanupRequestDto>(), Arg.Any<CancellationToken>()).ThrowsAsync(Refused("backup-running", "A backup is running."));

        var dialog = RenderDialog();

        dialog.Find("[data-testid=cleanup-error]").GetAttribute("data-code").ShouldBe("backup-running");
        dialog.FindAll("[data-testid=cleanup-plan-items]").ShouldBeEmpty();
    }

    [Fact]
    public async Task CancelAndEscape_CloseWithoutRemoving()
    {
        var dialog = RenderDialog();

        await dialog.Find("[data-testid=cleanup-dialog]").KeyDownAsync(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "a" });
        _closed.ShouldBeNull();
        await dialog.Find("[data-testid=cleanup-dialog]").KeyDownAsync(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Escape" });
        _closed.ShouldBe(false);
        await LibraryCare.DidNotReceive().ExecuteCleanupAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("cleanup-detail-stems", CleanupKindDto.DetailStems)]
    [InlineData("cleanup-worker-cache", CleanupKindDto.WorkerCache)]
    [InlineData("cleanup-leftovers", CleanupKindDto.Leftovers)]
    public async Task StorageButtons_OpenThePreviewForAllTracks(string id, CleanupKindDto kind)
    {
        var tab = Render<StorageTab>(p => p.Add(t => t.Status, LibraryCareData.Status()));

        await tab.Find($"[data-testid={id}]").ClickAsync(new());

        tab.Find("[data-testid=cleanup-dialog]").ShouldNotBeNull();
        await LibraryCare.Received(1).PreviewCleanupAsync(Arg.Is<CleanupRequestDto>(r => r.Actions.Single().Kind == kind && r.Actions.Single().TrackIds == null), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RowAction_PreviewsOneTrack_AndARemovalMeasuresAgain()
    {
        var tab = Render<StorageTab>(p => p.Add(t => t.Status, LibraryCareData.Status()));

        tab.FindAll($"[data-testid=cleanup-track-detail-{StorageData.TrackB}]").ShouldBeEmpty();
        await tab.Find($"[data-testid=cleanup-track-detail-{StorageData.TrackA}]").ClickAsync(new());
        await LibraryCare.Received(1).PreviewCleanupAsync(Arg.Is<CleanupRequestDto>(r => r.Actions.Single().TrackIds!.Single() == StorageData.TrackA), Arg.Any<CancellationToken>());
        await tab.Find("[data-testid=cleanup-confirm]").ClickAsync(new());
        await tab.Find("[data-testid=cleanup-dialog-cancel]").ClickAsync(new());

        tab.FindAll("[data-testid=cleanup-dialog]").ShouldBeEmpty();
        await LibraryCare.Received(1).GetStorageOverviewAsync(true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClosingWithoutRemoval_DoesNotMeasureAgain()
    {
        var tab = Render<StorageTab>(p => p.Add(t => t.Status, LibraryCareData.Status()));

        await tab.Find("[data-testid=cleanup-leftovers]").ClickAsync(new());
        await tab.Find("[data-testid=cleanup-dialog-cancel]").ClickAsync(new());

        await LibraryCare.DidNotReceive().GetStorageOverviewAsync(true, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(GateModeDto.BackupRunning, LibraryJobKindDto.Backup)]
    [InlineData(GateModeDto.Open, LibraryJobKindDto.Restore)]
    [InlineData(GateModeDto.Open, LibraryJobKindDto.UndoRestore)]
    [InlineData(GateModeDto.Maintenance, LibraryJobKindDto.Restore)]
    public void DuringABackupOrRestore_TheCleanUpsWait(GateModeDto gate, LibraryJobKindDto kind)
    {
        var tab = Render<StorageTab>(p => p.Add(t => t.Status, LibraryCareData.Status(LibraryCareData.Job(kind), gate)));

        foreach (var id in new[] { "cleanup-detail-stems", "cleanup-worker-cache", "cleanup-leftovers", $"cleanup-track-detail-{StorageData.TrackA}" })
        {
            tab.Find($"[data-testid={id}]").HasAttribute("disabled").ShouldBeTrue(id);
        }

        tab.Find("[data-testid=cleanup-leftovers]").GetAttribute("aria-describedby").ShouldBe("cleanup-blocked-reason");
        tab.Find("#cleanup-blocked-reason").TextContent.ShouldContain("waits until the running backup or restore has finished");
    }

    [Fact]
    public void DuringACheck_TheCleanUpsStayOn()
    {
        var tab = Render<StorageTab>(p => p.Add(t => t.Status, LibraryCareData.Status(LibraryCareData.Job(LibraryJobKindDto.Verify))));

        tab.Find("[data-testid=cleanup-leftovers]").HasAttribute("disabled").ShouldBeFalse();
        tab.FindAll("#cleanup-blocked-reason").ShouldBeEmpty();
    }

    [Theory]
    [InlineData(CleanupSkipReasonDto.JobActive, "a job uses it")]
    [InlineData(CleanupSkipReasonDto.NothingToRemove, "there is nothing to remove")]
    public void SkipReasons_HaveTexts(CleanupSkipReasonDto reason, string text) => CleanupText.Reason(reason).ShouldBe(text);

    [Theory]
    [InlineData(CleanupKindDto.DetailStems, "Detail stems")]
    [InlineData(CleanupKindDto.WorkerCache, "Worker cache")]
    public void NamesWithoutTitle_SayWhatItIs(CleanupKindDto kind, string name) => CleanupText.Name(kind, null).ShouldBe(name);
}
