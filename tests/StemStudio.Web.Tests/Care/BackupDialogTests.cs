using System.Net;
using Bunit;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Contracts.LibraryCare;
using StemStudio.Web.Api;
using StemStudio.Web.Components.Care;
using StemStudio.Web.Experience;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Care;

/// <summary>
/// The backup dialog (013 T039, US1, FR-001-FR-009, FR-031, FR-032; ui-test-ids 1.12.0 backup-*): profiles, options at
/// Advanced+, estimate rows, the voice notice, the space rule and the readable 409 reasons.
/// </summary>
public sealed class BackupDialogTests : PageTestContext
{
    private LibraryJobDto? _started;
    private bool _closed;

    private IRenderedComponent<BackupDialog> RenderDialog() =>
        Render<BackupDialog>(p => p.Add(d => d.OnStarted, job => _started = job).Add(d => d.OnClose, () => _closed = true));

    private IRenderedComponent<Microsoft.AspNetCore.Components.CascadingValue<ExperienceContext>> RenderAt(ExperienceLevel level) =>
        RenderAtLevel<BackupDialog>(level, p => p.Add(d => d.OnStarted, job => _started = job).Add(d => d.OnClose, () => _closed = true));

    private BackupOptionsDto LastEstimate() =>
        (BackupOptionsDto)LibraryCare.ReceivedCalls().Last(c => c.GetMethodInfo().Name == nameof(ILibraryCareClient.EstimateBackupAsync)).GetArguments()[0]!;

    [Fact]
    public void Dialog_IsAModal_LabelledByItsTitle_WithHelp()
    {
        var dialog = RenderDialog();

        var root = dialog.Find("[data-testid=backup-dialog]");
        (root.GetAttribute("role"), root.GetAttribute("aria-modal"), root.GetAttribute("aria-labelledby"), root.GetAttribute("data-help-id"))
            .ShouldBe(("dialog", "true", "backup-dialog-title", "backup-dialog"));
        dialog.Find("#backup-dialog-title").TextContent.ShouldBe("New backup");
        dialog.FindAll("[data-focus-sentinel]").Select(s => s.GetAttribute("tabindex")).ShouldBe(["0", "-1", "0"]);
        Interop.FocusCaptures.ShouldBe(1);
    }

    [Fact]
    public void Defaults_AreComplete_WithTheEstimate()
    {
        var dialog = RenderDialog();

        dialog.Find("[data-testid=backup-profile-complete]").HasAttribute("checked").ShouldBeTrue();
        dialog.Find("[data-testid=backup-profile-compact]").HasAttribute("checked").ShouldBeFalse();
        LastEstimate().ShouldBe(new BackupOptionsDto());
        var estimate = dialog.Find("[data-testid=backup-estimate]");
        (estimate.GetAttribute("data-total-bytes"), estimate.GetAttribute("data-free-bytes"), estimate.GetAttribute("data-enough"))
            .ShouldBe(("22548578304", "128849018880", "true"));
        estimate.TextContent.ShouldBe("About 21 GB; 120 GB free in the backups folder (enough space).");
        (estimate.GetAttribute("role"), estimate.GetAttribute("data-help-id")).ShouldBe(("status", "backup-estimate"));
        dialog.Find("[data-testid=backup-start]").HasAttribute("disabled").ShouldBeFalse();
    }

    [Fact]
    public async Task Compact_LeavesOutTheDetailStems_AndEstimatesAgain()
    {
        var dialog = RenderDialog();

        await dialog.Find("[data-testid=backup-profile-compact]").ChangeAsync(new() { Value = "on" });

        LastEstimate().ShouldBe(new BackupOptionsDto { DetailStems = false });
        dialog.Find("[data-testid=backup-profile-compact]").HasAttribute("checked").ShouldBeTrue();
        dialog.Find("[data-testid=backup-option-detail-stems]").HasAttribute("checked").ShouldBeFalse();

        await dialog.Find("[data-testid=backup-profile-complete]").ChangeAsync(new() { Value = "on" });
        LastEstimate().ShouldBe(new BackupOptionsDto());
    }

    [Theory]
    [InlineData(ExperienceLevel.Beginner, 0)]
    [InlineData(ExperienceLevel.Advanced, 1)]
    [InlineData(ExperienceLevel.Expert, 1)]
    public void Options_CustomProfileAndEstimateRows_AreShownFromAdvanced(ExperienceLevel level, int count)
    {
        var dialog = RenderAt(level);

        foreach (var id in new[] { "detail-stems", "exports", "scores", "generations", "recordings", "voice-profiles" })
        {
            dialog.FindAll($"[data-testid=backup-option-{id}]").Count.ShouldBe(count, id);
        }

        dialog.FindAll("[data-testid=backup-profile-custom]").Count.ShouldBe(count);
        dialog.FindAll("[data-testid=backup-estimate-detail-stems]").Count.ShouldBe(count);
        dialog.FindAll("[data-testid=backup-profile-complete], [data-testid=backup-profile-compact], [data-testid=backup-estimate]").Count.ShouldBe(3);
    }

    [Fact]
    public async Task ChangingAnOption_SelectsCustom_AndSendsTheOptions()
    {
        var dialog = RenderDialog();

        await dialog.Find("[data-testid=backup-option-exports]").ChangeAsync(new() { Value = false });

        dialog.Find("[data-testid=backup-profile-custom]").HasAttribute("checked").ShouldBeTrue();
        LastEstimate().ShouldBe(new BackupOptionsDto { Exports = false });
        foreach (var (id, expected) in new[]
        {
            ("detail-stems", new BackupOptionsDto { Exports = false, DetailStems = false }),
            ("scores", new BackupOptionsDto { Exports = false, DetailStems = false, Scores = false }),
            ("generations", new BackupOptionsDto { Exports = false, DetailStems = false, Scores = false, Generations = false }),
            ("recordings", new BackupOptionsDto { Exports = false, DetailStems = false, Scores = false, Generations = false, Recordings = false }),
            ("voice-profiles", new BackupOptionsDto { Exports = false, DetailStems = false, Scores = false, Generations = false, Recordings = false, VoiceProfiles = false }),
        })
        {
            await dialog.Find($"[data-testid=backup-option-{id}]").ChangeAsync(new() { Value = false });
            LastEstimate().ShouldBe(expected, id);
        }

        await dialog.Find("[data-testid=backup-profile-custom]").ChangeAsync(new() { Value = "on" });
        LastEstimate().VoiceProfiles.ShouldBeFalse();
    }

    [Fact]
    public async Task UntickingOnlyTheDetailStems_IsTheCompactProfile()
    {
        var dialog = RenderDialog();

        await dialog.Find("[data-testid=backup-option-detail-stems]").ChangeAsync(new() { Value = false });

        dialog.Find("[data-testid=backup-profile-compact]").HasAttribute("checked").ShouldBeTrue();
    }

    [Fact]
    public void EstimateRows_CarryBytesFilesClassAndIncluded()
    {
        LibraryCare.EstimateBackupAsync(Arg.Any<BackupOptionsDto>(), Arg.Any<CancellationToken>()).Returns(LibraryCareData.Estimate(detail: false));

        var dialog = RenderDialog();

        var row = dialog.Find("[data-testid=backup-estimate-detail-stems]");
        (row.GetAttribute("data-bytes"), row.GetAttribute("data-included")).ShouldBe(("600000000", "false"));
        row.TextContent.ShouldContain("Detail stems");
        row.TextContent.ShouldContain("572 MB");
        row.TextContent.ShouldContain("Regenerable");
        row.TextContent.ShouldContain("No");
        dialog.Find("[data-testid=backup-estimate-stems]").TextContent.ShouldContain("Original");
        dialog.Find("[data-testid=backup-estimate-database]").GetAttribute("data-included").ShouldBe("true");
    }

    [Fact]
    public void VoiceNotice_IsShown_WhenVoiceProfilesAreIncludedAndExist()
    {
        var dialog = RenderDialog();

        var notice = dialog.Find("[data-testid=backup-voice-notice]");
        notice.TextContent.ShouldContain("voice samples");
        notice.TextContent.ShouldContain("not encrypted");
        notice.TextContent.ShouldContain("protected drive");
        notice.GetAttribute("data-help-id").ShouldBe("backup-voice-notice");
    }

    [Fact]
    public async Task VoiceNotice_IsHidden_WithoutProfilesOrWhenLeftOut()
    {
        LibraryCare.EstimateBackupAsync(Arg.Any<BackupOptionsDto>(), Arg.Any<CancellationToken>()).Returns(LibraryCareData.Estimate(voiceFiles: 0));
        var dialog = RenderDialog();
        dialog.FindAll("[data-testid=backup-voice-notice]").ShouldBeEmpty();

        LibraryCare.EstimateBackupAsync(Arg.Any<BackupOptionsDto>(), Arg.Any<CancellationToken>()).Returns(LibraryCareData.Estimate());
        await dialog.Find("[data-testid=backup-option-voice-profiles]").ChangeAsync(new() { Value = false });
        dialog.FindAll("[data-testid=backup-voice-notice]").ShouldBeEmpty();
    }

    [Fact]
    public void NotEnoughSpace_DisablesStart()
    {
        LibraryCare.EstimateBackupAsync(Arg.Any<BackupOptionsDto>(), Arg.Any<CancellationToken>()).Returns(LibraryCareData.Estimate(enough: false) with
        {
            Folder = LibraryCareData.Folder(free: null),
        });

        var dialog = RenderDialog();

        var estimate = dialog.Find("[data-testid=backup-estimate]");
        estimate.GetAttribute("data-enough").ShouldBe("false");
        estimate.TextContent.ShouldContain("free space unknown (not enough space)");
        dialog.Find("[data-testid=backup-start]").HasAttribute("disabled").ShouldBeTrue();
    }

    [Fact]
    public void EstimateFailure_ShowsTheReason_AndStartStaysOff()
    {
        LibraryCare.EstimateBackupAsync(Arg.Any<BackupOptionsDto>(), Arg.Any<CancellationToken>()).ThrowsAsync(Problem(HttpStatusCode.ServiceUnavailable, "The API cannot be reached."));

        var dialog = RenderDialog();

        dialog.Find("[data-testid=backup-error]").TextContent.ShouldBe("The API cannot be reached.");
        dialog.FindAll("[data-testid=backup-estimate]").ShouldBeEmpty();
        dialog.Markup.ShouldNotContain("Estimating");
        dialog.Find("[data-testid=backup-start]").HasAttribute("disabled").ShouldBeTrue();
    }

    [Fact]
    public void WhileEstimating_SaysSo()
    {
        LibraryCare.EstimateBackupAsync(Arg.Any<BackupOptionsDto>(), Arg.Any<CancellationToken>()).Returns(new TaskCompletionSource<BackupEstimateDto>().Task);

        var dialog = RenderDialog();

        dialog.Find("[role=status]").TextContent.ShouldBe("Estimating the size...");
        dialog.Find("[data-testid=backup-start]").HasAttribute("disabled").ShouldBeTrue();
    }

    [Fact]
    public async Task AStaleEstimate_IsIgnored()
    {
        var first = new TaskCompletionSource<BackupEstimateDto>();
        LibraryCare.EstimateBackupAsync(Arg.Any<BackupOptionsDto>(), Arg.Any<CancellationToken>()).Returns(first.Task, Task.FromResult(LibraryCareData.Estimate(detail: false)));
        var dialog = RenderDialog();

        await dialog.Find("[data-testid=backup-profile-compact]").ChangeAsync(new() { Value = "on" });
        await dialog.InvokeAsync(() => first.SetResult(LibraryCareData.Estimate(enough: false)));

        dialog.Find("[data-testid=backup-estimate]").GetAttribute("data-enough").ShouldBe("true");
        dialog.Find("[data-testid=backup-estimate-detail-stems]").GetAttribute("data-included").ShouldBe("false");
    }

    [Fact]
    public async Task Start_SendsTheOptions_AndReportsTheJob()
    {
        var job = LibraryCareData.Job(status: LibraryJobStatusDto.Queued);
        LibraryCare.CreateBackupAsync(Arg.Any<BackupOptionsDto>(), Arg.Any<CancellationToken>()).Returns(job);
        var dialog = RenderDialog();

        await dialog.Find("[data-testid=backup-profile-compact]").ChangeAsync(new() { Value = "on" });
        await dialog.Find("[data-testid=backup-start]").ClickAsync(new());

        await LibraryCare.Received(1).CreateBackupAsync(new BackupOptionsDto { DetailStems = false }, Arg.Any<CancellationToken>());
        _started.ShouldBe(job);
    }

    [Theory]
    [InlineData("library-job-running", "Another library job is running.")]
    [InlineData("backups-folder-unavailable", "The backups folder is not writable.")]
    [InlineData("insufficient-disk-space", "The backup needs about 21.4 GB; 12.0 GB is free.")]
    public async Task Start_Refused_ShowsTheReadableReasonWithItsCode(string code, string detail)
    {
        LibraryCare.CreateBackupAsync(Arg.Any<BackupOptionsDto>(), Arg.Any<CancellationToken>()).ThrowsAsync(new ApiException(
            new ApiError(HttpStatusCode.Conflict, "Refused", detail, new Dictionary<string, string[]>()) { Type = $"https://stemstudio.local/problems/{code}" }));
        var dialog = RenderDialog();

        await dialog.Find("[data-testid=backup-start]").ClickAsync(new());

        var error = dialog.Find("[data-testid=backup-error]");
        (error.TextContent, error.GetAttribute("data-code"), error.GetAttribute("role")).ShouldBe((detail, code, "alert"));
        _started.ShouldBeNull();
        dialog.Find("[data-testid=backup-start]").HasAttribute("disabled").ShouldBeFalse();
    }

    [Fact]
    public async Task Escape_Closes_OtherKeysDoNot()
    {
        var dialog = RenderDialog();

        await dialog.Find("[data-testid=backup-dialog]").KeyDownAsync(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Enter" });
        _closed.ShouldBeFalse();
        await dialog.Find("[data-testid=backup-dialog]").KeyDownAsync(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Escape" });
        _closed.ShouldBeTrue();
    }

    [Fact]
    public async Task FocusTrap_WrapsBothWays()
    {
        var dialog = RenderDialog();

        await dialog.Find("[data-focus-sentinel=end]").FocusAsync(new());
        await dialog.Find("[data-focus-sentinel=start]").FocusAsync(new());

        JSInterop.Invocations.Count(i => i.Identifier == "Blazor._internal.domWrapper.focus").ShouldBeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public void FocusTrapScript_WrapsInTheBrowser_AndIsLoadedByTheApp()
    {
        // 013 CTO a11y finding: the sentinels' server round trip let a fast second Tab leave the dialog.
        var script = RepoPaths.ReadWeb("wwwroot/js/focus-trap.js");
        var app = RepoPaths.ReadWeb("Components/App.razor");

        script.ShouldContain("addEventListener('focusin'");
        script.ShouldContain("'[data-focus-sentinel]'");
        script.ShouldContain("getAttribute('aria-labelledby')");
        script.ShouldContain("'[data-focus-sentinel=\"end-anchor\"]'");
        script.ShouldNotContain("setTimeout");
        app.ShouldContain("<script src=\"js/focus-trap.js\"></script>");
    }
}
