using System.Net;
using System.Text.Json;
using Bunit;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Contracts.LibraryCare;
using StemStudio.Web.Api;
using StemStudio.Web.Components.Care;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Care;

/// <summary>
/// The restore dialog (013 T052, US2, FR-010-FR-018, FR-031, FR-032; ui-test-ids 1.12.0 restore-*): empty library, replace
/// with the typed REPLACE, voice profiles, space, blockers with links and the readable reasons.
/// </summary>
public sealed class RestoreDialogTests : PageTestContext
{
    private static readonly long Free = 20L * 1024 * 1024 * 1024;
    private LibraryJobDto? _started;
    private bool _closed;

    public RestoreDialogTests() =>
        LibraryCare.GetStorageOverviewAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(StorageData.Overview(libraryFree: Free));

    private IRenderedComponent<RestoreDialog> RenderDialog(LibraryCareStatusDto? status = null, BackupSummaryDto? archive = null) =>
        Render<RestoreDialog>(p => p
            .Add(d => d.Archive, archive ?? LibraryCareData.Summary())
            .Add(d => d.Status, status ?? LibraryCareData.Status())
            .Add(d => d.OnStarted, job => _started = job)
            .Add(d => d.OnClose, () => _closed = true));

    private static bool StartDisabled(IRenderedComponent<RestoreDialog> dialog) => dialog.Find("[data-testid=restore-start]").HasAttribute("disabled");

    private RestoreOptionsDto Sent() =>
        (RestoreOptionsDto)LibraryCare.ReceivedCalls().Single(c => c.GetMethodInfo().Name == nameof(ILibraryCareClient.RestoreBackupAsync)).GetArguments()[1]!;

    [Fact]
    public void Dialog_IsAModal_WithHelp_AndSaysItChecksFirst()
    {
        var dialog = RenderDialog();

        var root = dialog.Find("[data-testid=restore-dialog]");
        (root.GetAttribute("role"), root.GetAttribute("aria-modal"), root.GetAttribute("aria-labelledby"), root.GetAttribute("data-help-id"))
            .ShouldBe(("dialog", "true", "restore-dialog-title", "restore-dialog"));
        dialog.Find("#restore-dialog-subject").TextContent.ShouldContain("checked before anything in your library changes");
        dialog.FindAll("[data-focus-sentinel]").Count.ShouldBe(3);
    }

    [Fact]
    public async Task EmptyLibrary_HasNoModeAndNoConfirmation_AndRestoresAtOnce()
    {
        LibraryCare.RestoreBackupAsync(LibraryCareData.Archive, Arg.Any<RestoreOptionsDto>(), Arg.Any<CancellationToken>())
            .Returns(LibraryCareData.Job(LibraryJobKindDto.Restore, LibraryJobStatusDto.Queued, null, 0));
        var dialog = RenderDialog(LibraryCareData.Status(empty: true));

        dialog.Find("[data-testid=restore-empty]").TextContent.ShouldBe("Your library is empty; the backup will be restored.");
        dialog.FindAll("[data-testid=restore-mode-replace], [data-testid=restore-mode-merge], [data-testid=restore-confirm-input]").ShouldBeEmpty();
        StartDisabled(dialog).ShouldBeFalse();
        await dialog.Find("[data-testid=restore-start]").ClickAsync(new());

        Sent().ShouldBe(new RestoreOptionsDto { Mode = RestoreModeDto.Replace, VoiceProfiles = true, Confirmation = null });
        _started!.Kind.ShouldBe(LibraryJobKindDto.Restore);
    }

    [Fact]
    public async Task Replace_NeedsTheTypedWord()
    {
        LibraryCare.RestoreBackupAsync(Arg.Any<string>(), Arg.Any<RestoreOptionsDto>(), Arg.Any<CancellationToken>()).Returns(LibraryCareData.Job(LibraryJobKindDto.Restore));
        var dialog = RenderDialog();

        dialog.Find("[data-testid=restore-mode-replace]").HasAttribute("checked").ShouldBeTrue();
        dialog.Find("label[for=restore-confirm-input]").TextContent.ShouldContain("Type REPLACE");
        StartDisabled(dialog).ShouldBeTrue();
        await dialog.Find("[data-testid=restore-confirm-input]").InputAsync(new() { Value = "replace" });
        StartDisabled(dialog).ShouldBeTrue();
        await dialog.Find("[data-testid=restore-confirm-input]").InputAsync(new() { Value = "REPLACE" });
        StartDisabled(dialog).ShouldBeFalse();
        await dialog.Find("[data-testid=restore-mode-replace]").ChangeAsync(new() { Value = "on" });
        await dialog.Find("[data-testid=restore-start]").ClickAsync(new());

        Sent().ShouldBe(new RestoreOptionsDto { Mode = RestoreModeDto.Replace, VoiceProfiles = true, Confirmation = "REPLACE" });
    }

    [Fact]
    public async Task VoiceProfiles_CanBeLeftOut()
    {
        LibraryCare.RestoreBackupAsync(Arg.Any<string>(), Arg.Any<RestoreOptionsDto>(), Arg.Any<CancellationToken>()).Returns(LibraryCareData.Job(LibraryJobKindDto.Restore));
        var dialog = RenderDialog(LibraryCareData.Status(empty: true));

        var box = dialog.Find("[data-testid=restore-voice-profiles]");
        box.HasAttribute("checked").ShouldBeTrue();
        dialog.Find("[data-help-id=restore-voice-profiles]").TextContent.ShouldContain("valid consent record");
        await box.ChangeAsync(new() { Value = false });
        await dialog.Find("[data-testid=restore-start]").ClickAsync(new());

        Sent().VoiceProfiles.ShouldBeFalse();
    }

    [Fact]
    public void VoiceProfiles_CheckboxIsHidden_WithoutProfiles()
    {
        LibraryCare.GetBackupAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(LibraryCareData.Details() with { VoiceProfiles = [], Counts = LibraryCareData.Counts(voices: 0) });

        RenderDialog().FindAll("[data-testid=restore-voice-profiles]").ShouldBeEmpty();
    }

    [Fact]
    public void VoiceProfiles_CountedWithoutProofs_StillShowTheCheckbox()
    {
        LibraryCare.GetBackupAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(LibraryCareData.Details() with { VoiceProfiles = [] });

        RenderDialog().FindAll("[data-testid=restore-voice-profiles]").Count.ShouldBe(1);
    }

    [Fact]
    public void Space_ShowsNeededAndFree()
    {
        var space = RenderDialog().Find("[data-testid=restore-space]");

        space.GetAttribute("data-enough").ShouldBe("true");
        space.TextContent.ShouldBe("Needs about 3.3 GB; 20 GB free in the library.");
    }

    [Fact]
    public async Task NotEnoughSpace_KeepsStartOff()
    {
        LibraryCare.GetStorageOverviewAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(StorageData.Overview(libraryFree: 1024L * 1024 * 1024));
        var dialog = RenderDialog();

        await dialog.Find("[data-testid=restore-confirm-input]").InputAsync(new() { Value = "REPLACE" });

        var space = dialog.Find("[data-testid=restore-space]");
        (space.GetAttribute("data-enough"), space.TextContent).ShouldBe(("false", "Needs about 3.3 GB; 1.0 GB free in the library: not enough space."));
        StartDisabled(dialog).ShouldBeTrue();
    }

    [Fact]
    public void UnknownFreeSpace_DoesNotBlock()
    {
        LibraryCare.GetStorageOverviewAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>()).ThrowsAsync(Problem(HttpStatusCode.ServiceUnavailable, "Down"));

        var dialog = RenderDialog(LibraryCareData.Status(empty: true));

        var space = dialog.Find("[data-testid=restore-space]");
        (space.GetAttribute("data-enough"), space.TextContent).ShouldBe(("true", "Needs about 3.3 GB; the free space of the library is not known."));
        StartDisabled(dialog).ShouldBeFalse();
    }

    [Fact]
    public void DetailsFailure_ShowsTheReason_AndStartStaysOff()
    {
        LibraryCare.GetBackupAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).ThrowsAsync(Problem(HttpStatusCode.UnprocessableEntity, "The archive cannot be read."));

        var dialog = RenderDialog(LibraryCareData.Status(empty: true));

        dialog.Find("[data-testid=restore-error]").TextContent.ShouldBe("The archive cannot be read.");
        dialog.Find("[data-testid=restore-space]").TextContent.ShouldBe("Checking the size...");
        StartDisabled(dialog).ShouldBeTrue();
    }

    [Fact]
    public void ActiveJobs_AreListedWithLinks_AndBlockTheRestore()
    {
        var trackId = Guid.Parse("e1b2c3d4-0000-4000-8000-000000000001");
        var dialog = RenderDialog(LibraryCareData.Status(empty: true) with
        {
            ActiveWork =
            [
                new ActiveWorkDto { Kind = ActiveWorkKindDto.Export, Id = "2f6d8a0c-1b3e-4f5a-9c7d-8e9f0a1b2c3d", Title = "Song title - vocals" },
                new ActiveWorkDto { Kind = ActiveWorkKindDto.Detail, Id = trackId.ToString(), Title = "Song title" },
                new ActiveWorkDto { Kind = ActiveWorkKindDto.LyricsAlignment, Id = "not-a-guid", Title = "Song" },
                new ActiveWorkDto { Kind = ActiveWorkKindDto.Generation, Id = "g1", Title = "Restyle of Song title" },
                new ActiveWorkDto { Kind = ActiveWorkKindDto.AgentRun, Id = "r1", Title = "Make the drums louder" },
            ],
        });

        dialog.Find("[data-testid=restore-blockers]").TextContent.ShouldContain("Wait for them or cancel them");
        dialog.Find("[data-testid=restore-blocker-export-2f6d8a0c-1b3e-4f5a-9c7d-8e9f0a1b2c3d] a").GetAttribute("href").ShouldBe("library");
        dialog.Find($"[data-testid=restore-blocker-detail-{trackId}] a").GetAttribute("href").ShouldBe($"tracks/{trackId}");
        dialog.Find($"[data-testid=restore-blocker-detail-{trackId}]").TextContent.ShouldBe("Split further: Song title");
        dialog.Find("[data-testid=restore-blocker-lyrics-alignment-not-a-guid] a").GetAttribute("href").ShouldBe("tracks");
        dialog.Find("[data-testid=restore-blocker-generation-g1] a").GetAttribute("href").ShouldBe("generations");
        dialog.Find("[data-testid=restore-blocker-agent-run-r1]").TextContent.ShouldContain("composer panel");
        dialog.FindAll("[data-testid=restore-blocker-agent-run-r1] a").ShouldBeEmpty();
        StartDisabled(dialog).ShouldBeTrue();
    }

    [Theory]
    [InlineData(ActiveWorkKindDto.Import, "Import")]
    [InlineData(ActiveWorkKindDto.Score, "Sheet music")]
    public void BlockerKinds_HaveNames(ActiveWorkKindDto kind, string name) => Web.LibraryCare.RestoreForm.BlockerKind(kind).ShouldBe(name);

    [Fact]
    public void KeptPreviousLibrary_BlocksAReplace()
    {
        var dialog = RenderDialog(LibraryCareData.Status(previous: LibraryCareData.Previous()));

        dialog.Markup.ShouldContain("Undo the last restore or discard the previous library first.");
        StartDisabled(dialog).ShouldBeTrue();
    }

    [Fact]
    public async Task JobsActiveAnswer_ShowsTheReason_AndListsTheJobs()
    {
        var work = JsonSerializer.SerializeToElement(new[] { new { kind = "score", id = "s1", title = "Song - piano" } });
        LibraryCare.RestoreBackupAsync(Arg.Any<string>(), Arg.Any<RestoreOptionsDto>(), Arg.Any<CancellationToken>()).ThrowsAsync(new ApiException(
            new ApiError(HttpStatusCode.Conflict, "Jobs are running", "Wait for 1 job to finish or cancel it, then restore.", new Dictionary<string, string[]>())
            {
                Type = "https://stemstudio.local/problems/jobs-active",
                Extensions = new Dictionary<string, JsonElement> { ["activeWork"] = work },
            }));
        var dialog = RenderDialog(LibraryCareData.Status(empty: true));

        await dialog.Find("[data-testid=restore-start]").ClickAsync(new());

        var error = dialog.Find("[data-testid=restore-error]");
        (error.TextContent, error.GetAttribute("data-code")).ShouldBe(("Wait for 1 job to finish or cancel it, then restore.", "jobs-active"));
        dialog.Find("[data-testid=restore-blocker-score-s1] a").GetAttribute("href").ShouldBe("tracks");
        _started.ShouldBeNull();
    }

    [Theory]
    [InlineData(HttpStatusCode.Conflict, "previous-library-exists")]
    [InlineData(HttpStatusCode.UnprocessableEntity, "archive-incompatible")]
    [InlineData(HttpStatusCode.Conflict, "jobs-active")]
    public async Task OtherRefusals_ShowTheirCode(HttpStatusCode status, string code)
    {
        LibraryCare.RestoreBackupAsync(Arg.Any<string>(), Arg.Any<RestoreOptionsDto>(), Arg.Any<CancellationToken>()).ThrowsAsync(new ApiException(
            new ApiError(status, "Refused", "Not now.", new Dictionary<string, string[]>()) { Type = $"https://stemstudio.local/problems/{code}" }));
        var dialog = RenderDialog(LibraryCareData.Status(empty: true));

        await dialog.Find("[data-testid=restore-start]").ClickAsync(new());

        dialog.Find("[data-testid=restore-error]").GetAttribute("data-code").ShouldBe(code);
        dialog.FindAll("[data-testid=restore-blockers]").ShouldBeEmpty();
    }

    [Fact]
    public async Task CancelAndEscape_Close()
    {
        var dialog = RenderDialog();

        await dialog.Find("[data-testid=restore-dialog-cancel]").ClickAsync(new());
        _closed.ShouldBeTrue();
        _closed = false;
        await dialog.Find("[data-testid=restore-dialog]").KeyDownAsync(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Escape" });
        _closed.ShouldBeTrue();
    }
}
