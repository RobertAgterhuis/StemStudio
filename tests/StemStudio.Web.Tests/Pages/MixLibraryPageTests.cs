using System.Net;
using Bunit;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Contracts.Mixes;
using StemStudio.Web.Components.Pages;
using StemStudio.Web.Mixer;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Pages;

public sealed class MixLibraryPageTests : PageTestContext
{
    private static readonly Guid OtherId = Guid.Parse("12121212-3434-5656-7878-909090909090");

    public MixLibraryPageTests() =>
        Api.ListMixesAsync(null, Arg.Any<CancellationToken>()).Returns([TestData.Summary(), TestData.Summary(OtherId, "Drums only")]);

    [Fact]
    public void Rows_ShowNameTrackAndLastModified()
    {
        var page = Render<Mixes>();

        var row = page.Find($"[data-testid=mix-row-{TestData.MixId}]");
        row.TextContent.ShouldContain("Guitar practice");
        row.TextContent.ShouldContain("Never Gonna Give You Up");
        row.QuerySelector("time")!.GetAttribute("datetime").ShouldBe("2026-11-14T13:00:00Z");
        page.Find($"[data-testid=mix-row-{OtherId}]").TextContent.ShouldContain("Drums only");
    }

    [Fact]
    public async Task Load_NavigatesToMixer()
    {
        var page = Render<Mixes>();

        await page.Find($"[data-testid=mix-load-{TestData.MixId}]").ClickAsync(new());

        NavigatedTo.ShouldBe($"http://localhost/tracks/{TestData.TrackId}/mixer?mixId={TestData.MixId}");
    }

    [Fact]
    public async Task Duplicate_CallsApi_AndRefreshes()
    {
        Api.DuplicateMixAsync(TestData.MixId, Arg.Any<CancellationToken>()).Returns(TestData.Mix("Guitar practice (copy)"));
        var page = Render<Mixes>();

        await page.Find($"[data-testid=mix-duplicate-{TestData.MixId}]").ClickAsync(new());

        await Api.Received(1).DuplicateMixAsync(TestData.MixId, Arg.Any<CancellationToken>());
        await Api.Received(2).ListMixesAsync(null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_AsksForConfirmation_ThenDeletes()
    {
        Confirm.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        var page = Render<Mixes>();
        Api.ListMixesAsync(null, Arg.Any<CancellationToken>()).Returns([TestData.Summary(OtherId, "Drums only")]);

        await page.Find($"[data-testid=mix-delete-{TestData.MixId}]").ClickAsync(new());

        await Confirm.Received(1).ConfirmAsync("Delete mix", Arg.Is<string>(m => m.Contains("Guitar practice") && m.Contains("track and its stems remain")), "Delete");
        await Api.Received(1).DeleteMixAsync(TestData.MixId, Arg.Any<CancellationToken>());
        page.FindAll($"[data-testid=mix-row-{TestData.MixId}]").ShouldBeEmpty();
    }

    [Fact]
    public async Task Delete_Cancelled_KeepsMix()
    {
        Confirm.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(false);
        var page = Render<Mixes>();

        await page.Find($"[data-testid=mix-delete-{TestData.MixId}]").ClickAsync(new());

        await Api.DidNotReceiveWithAnyArgs().DeleteMixAsync(default, TestContext.Current.CancellationToken);
        page.Find($"[data-testid=mix-row-{TestData.MixId}]").ShouldNotBeNull();
    }

    [Fact]
    public async Task Rename_PutsExistingSettingsWithNewName()
    {
        Api.GetMixAsync(TestData.MixId, Arg.Any<CancellationToken>()).Returns(TestData.Mix());
        Api.UpdateMixAsync(TestData.MixId, Arg.Any<MixSettingsDto>(), Arg.Any<CancellationToken>()).Returns(TestData.Mix("Solo work"));
        var page = Render<Mixes>();

        await page.Find($"[data-testid=mix-rename-{TestData.MixId}]").ClickAsync(new());
        page.Find("[data-testid=mix-name-input]").GetAttribute("value").ShouldBe("Guitar practice");
        page.Find("[data-testid=mix-name-input]").Input("Solo work");
        await page.Find("[data-testid=mix-save-confirm]").ClickAsync(new());

        await Api.Received(1).UpdateMixAsync(TestData.MixId, TestData.Settings("Solo work"), Arg.Any<CancellationToken>());
        page.FindAll("[data-testid=mix-name-input]").ShouldBeEmpty();
        await Api.Received(2).ListMixesAsync(null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rename_NameTaken_ShowsMessage()
    {
        Api.GetMixAsync(TestData.MixId, Arg.Any<CancellationToken>()).Returns(TestData.Mix());
        Api.UpdateMixAsync(TestData.MixId, Arg.Any<MixSettingsDto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(Problem(HttpStatusCode.Conflict, "Mix name taken"));
        var page = Render<Mixes>();

        await page.Find($"[data-testid=mix-rename-{TestData.MixId}]").ClickAsync(new());
        page.Find("[data-testid=mix-name-input]").Input("Drums only");
        await page.Find("[data-testid=mix-save-confirm]").ClickAsync(new());

        page.Find("[data-testid=mix-error]").TextContent.ShouldBe(MixNames.TakenMessage("Drums only"));
    }

    [Fact]
    public async Task Rename_Cancel_ClosesDialog()
    {
        var page = Render<Mixes>();

        await page.Find($"[data-testid=mix-rename-{TestData.MixId}]").ClickAsync(new());
        await page.Find("[data-testid=mix-save-cancel]").ClickAsync(new());

        page.FindAll("[data-testid=mix-name-input]").ShouldBeEmpty();
    }

    [Fact]
    public async Task ActionFailure_IsShown()
    {
        Api.DuplicateMixAsync(TestData.MixId, Arg.Any<CancellationToken>()).ThrowsAsync(Problem(HttpStatusCode.NotFound, "Not found", "Mix does not exist."));
        var page = Render<Mixes>();

        await page.Find($"[data-testid=mix-duplicate-{TestData.MixId}]").ClickAsync(new());

        page.Find("[data-testid=mixes-error]").TextContent.ShouldBe("Mix does not exist.");
    }

    [Fact]
    public void Empty_And_LoadError_States()
    {
        Api.ListMixesAsync(null, Arg.Any<CancellationToken>()).Returns([]);
        Render<Mixes>().Find("[data-testid=mixes-empty]").TextContent.ShouldContain("No saved mixes");

        Api.ListMixesAsync(null, Arg.Any<CancellationToken>()).ThrowsAsync(Problem(HttpStatusCode.ServiceUnavailable, "API down"));
        Render<Mixes>().Find("[data-testid=mixes-error]").TextContent.ShouldBe("API down");

        Api.ListMixesAsync(null, Arg.Any<CancellationToken>()).Returns(new TaskCompletionSource<IReadOnlyList<MixSummaryDto>>().Task);
        Render<Mixes>().Markup.ShouldContain("Loading...");
    }
}
