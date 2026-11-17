using System.Net;
using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Contracts.LibraryCare;
using StemStudio.Web.Components.Layout;
using StemStudio.Web.LibraryCare;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Care;

/// <summary>
/// The shell's part of library care (013 T040, FR-006, FR-013, FR-032; ui-test-ids 1.12.0): nav-care, the library job
/// indicator, the maintenance overlay and the status polling (2 s while busy, 10 s while idle).
/// </summary>
public sealed class LibraryCareShellTests : PageTestContext
{
    private static RenderFragment Body => b => b.AddMarkupContent(0, "<h1>Page</h1>");

    private IRenderedComponent<MainLayout> RenderShell() => Render<MainLayout>(p => p.Add(l => l.Body, Body));

    private static IElement Id(IRenderedComponent<MainLayout> shell, string id) => shell.Find($"[data-testid={id}]");

    private int StatusReads => LibraryCare.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(Web.Api.ILibraryCareClient.GetStatusAsync));

    [Fact]
    public void Rail_LinksToLibraryCare()
    {
        var link = Id(RenderShell(), "nav-care");

        link.GetAttribute("href").ShouldBe("care");
        link.TextContent.ShouldContain("Library care");
    }

    [Fact]
    public void Idle_HasNoIndicatorAndNoOverlay()
    {
        var shell = RenderShell();

        shell.FindAll("[data-testid=library-job-indicator], [data-testid=library-maintenance]").ShouldBeEmpty();
    }

    [Fact]
    public void RunningJob_ShowsTheIndicator_LinkingToCare_WithKindAndProgress()
    {
        LibraryCare.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(LibraryCareData.Status(LibraryCareData.Job(), GateModeDto.BackupRunning));

        var shell = RenderShell();

        var indicator = shell.WaitForElement("[data-testid=library-job-indicator]");
        (indicator.TagName, indicator.GetAttribute("href"), indicator.GetAttribute("data-kind"), indicator.GetAttribute("data-progress"))
            .ShouldBe(("A", "care", "backup", "42"));
        indicator.TextContent.ShouldContain("Backup 42 %");
        indicator.GetAttribute("aria-label").ShouldBe("Backup 42 % (running). Open Library care");
        shell.Find("header").Contains(indicator).ShouldBeTrue();
    }

    [Fact]
    public void Polling_Is2sWhileBusy_And10sWhileIdle()
    {
        LibraryCare.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(LibraryCareData.Status(LibraryCareData.Job()), LibraryCareData.Status());
        var shell = RenderShell();
        shell.WaitForAssertion(() => StatusReads.ShouldBe(1));

        Time.Advance(TimeSpan.FromSeconds(2));
        shell.WaitForAssertion(() => StatusReads.ShouldBe(2));
        shell.WaitForAssertion(() => shell.FindAll("[data-testid=library-job-indicator]").ShouldBeEmpty());

        Time.Advance(TimeSpan.FromSeconds(9));
        StatusReads.ShouldBe(2);
        Time.Advance(TimeSpan.FromSeconds(1));
        shell.WaitForAssertion(() => StatusReads.ShouldBe(3));
    }

    [Fact]
    public void Maintenance_ShowsAnAlertDialog_WithTheFocus_ThatDisappearsByItself()
    {
        LibraryCare.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(
            LibraryCareData.Status(LibraryCareData.Job(LibraryJobKindDto.Restore, stage: LibraryJobStageDto.Switch, cancellable: false), GateModeDto.Maintenance),
            LibraryCareData.Status());
        var shell = RenderShell();

        var overlay = shell.WaitForElement("[data-testid=library-maintenance]");
        (overlay.GetAttribute("role"), overlay.GetAttribute("aria-modal"), overlay.GetAttribute("aria-labelledby"))
            .ShouldBe(("alertdialog", "true", "library-maintenance-title"));
        overlay.TextContent.ShouldContain("Switching to the restored library");
        Interop.FocusCaptures.ShouldBe(1);
        JSInterop.Invocations.ShouldContain(i => i.Identifier == "Blazor._internal.domWrapper.focus");

        Time.Advance(TimeSpan.FromSeconds(2));

        shell.WaitForAssertion(() => shell.FindAll("[data-testid=library-maintenance]").ShouldBeEmpty());
    }

    [Fact]
    public void StatusFailure_KeepsTheLastStatus()
    {
        LibraryCare.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(
            Task.FromResult(LibraryCareData.Status(LibraryCareData.Job())),
            Task.FromException<LibraryCareStatusDto>(Problem(HttpStatusCode.ServiceUnavailable, "Down")));
        var shell = RenderShell();
        shell.WaitForElement("[data-testid=library-job-indicator]");

        Time.Advance(TimeSpan.FromSeconds(2));

        shell.WaitForAssertion(() => StatusReads.ShouldBe(2));
        Id(shell, "library-job-indicator").ShouldNotBeNull();
    }

    [Fact]
    public async Task Source_Start_IsIdempotent_RefreshRaisesChanged_AndDisposeStopsPolling()
    {
        var source = Services.GetRequiredService<LibraryCareStatusSource>();
        var changes = 0;
        source.Changed += () => changes++;

        source.Gate.ShouldBe(GateModeDto.Open);
        source.Busy.ShouldBeFalse();
        source.Start();
        source.Start();
        StatusReads.ShouldBe(1);
        (await source.RefreshAsync(Xunit.TestContext.Current.CancellationToken)).ShouldNotBeNull();
        changes.ShouldBe(2);

        await source.DisposeAsync();
        Time.Advance(TimeSpan.FromSeconds(30));
        StatusReads.ShouldBe(2);
    }

    [Fact]
    public async Task Source_DisposedBeforeStart_DoesNotThrow()
    {
        var source = new LibraryCareStatusSource(LibraryCare, Time);

        await source.DisposeAsync();

        StatusReads.ShouldBe(0);
    }

    [Fact]
    public async Task Source_RefreshFailure_ReturnsTheLastStatus()
    {
        var source = new LibraryCareStatusSource(LibraryCare, Time);
        LibraryCare.GetStatusAsync(Arg.Any<CancellationToken>()).ThrowsAsync(Problem(HttpStatusCode.ServiceUnavailable, "Down"));

        (await source.RefreshAsync(Xunit.TestContext.Current.CancellationToken)).ShouldBeNull();
        source.ActiveJob.ShouldBeNull();
        await source.DisposeAsync();
    }
}
