using System.Net;
using Bunit;
using Microsoft.AspNetCore.Components;
using NSubstitute;
using StemStudio.Contracts.Assets;
using StemStudio.Web.Components.Library;
using StemStudio.Web.Experience;
using StemStudio.Web.Preferences;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Library;

/// <summary>Filters, rename, delete, retry, gain note and source of library items (007 T051, FR-005, FR-007, FR-008).</summary>
public sealed class LibraryManagementTests : PageTestContext
{
    private static readonly Guid DrumsId = Guid.Parse("7d4b2c8e-6a1f-4b3d-9e5c-2a3b4c5d6e7f");
    private static readonly Guid ElsewhereId = Guid.Parse("8e4b2c8e-6a1f-4b3d-9e5c-2a3b4c5d6e7f");

    private static readonly AssetDto Bass = AssetData.Asset();
    private static readonly AssetDto Drums = AssetData.Asset(DrumsId, name: "Drums for practice") with
    {
        Provenance = AssetData.Provenance(contents: "drums, raw"),
        Audio = AssetData.Facts(-1.8),
    };

    private static readonly AssetDto Elsewhere = AssetData.Asset(ElsewhereId, name: "Hero - vocals") with
    {
        Provenance = AssetData.Provenance(AssetData.OtherTrackId, "Hero of the Day", available: false, contents: "vocals, raw"),
    };

    private IRenderedComponent<CascadingValue<ExperienceContext>> RenderAt(ExperienceLevel level, params AssetDto[] items)
    {
        Api.ListAssetsAsync(Arg.Any<CancellationToken>()).Returns(AssetData.List(items.Length == 0 ? [Bass, Drums, Elsewhere] : items));
        return Render<CascadingValue<ExperienceContext>>(p => p
            .Add(c => c.Value, new ExperienceContext(level, DensityPreference.Comfortable))
            .AddChildContent<AllItemsView>());
    }

    private static IEnumerable<string?> Rows(IRenderedComponent<CascadingValue<ExperienceContext>> page) =>
        page.FindAll("[data-testid^=asset-row-]").Select(r => r.GetAttribute("data-testid")!["asset-row-".Length..]);

    [Fact]
    public async Task TrackFilter_ListsEverySourceTrack_AndNarrowsTheList()
    {
        var page = RenderAt(ExperienceLevel.Advanced);
        var filter = page.Find("[data-testid=library-filter-track]");
        filter.TagName.ShouldBe("SELECT");
        filter.QuerySelectorAll("option").Select(o => (o.GetAttribute("value"), o.TextContent)).ShouldBe(
            [(string.Empty, "All tracks"), (AssetData.OtherTrackId.ToString(), "Hero of the Day (deleted)"), (TestData.TrackId.ToString(), "Never Gonna Give You Up")]);

        await filter.ChangeAsync(new ChangeEventArgs { Value = AssetData.OtherTrackId.ToString() });

        Rows(page).ShouldBe([ElsewhereId.ToString()]);
    }

    [Fact]
    public async Task KindFilter_AndSearch_NarrowTheList()
    {
        var page = RenderAt(ExperienceLevel.Expert);

        await page.Find("[data-testid=library-filter-kind]").ChangeAsync(new ChangeEventArgs { Value = "score" });
        page.Find("[data-testid=library-empty]").TextContent.ShouldContain("No items match");
        await page.Find("[data-testid=library-filter-kind]").ChangeAsync(new ChangeEventArgs { Value = "audio" });
        await page.Find("[data-testid=library-search]").InputAsync(new ChangeEventArgs { Value = "PRACTICE" });

        Rows(page).ShouldBe([DrumsId.ToString()]);
    }

    [Fact]
    public void Beginner_HasNoFilters()
    {
        var page = RenderAt(ExperienceLevel.Beginner);

        page.FindAll("[data-testid=library-filter-track], [data-testid=library-filter-kind], [data-testid=library-search]").ShouldBeEmpty();
        page.FindAll("[data-testid^=asset-rename-]").Count.ShouldBe(3);
    }

    [Fact]
    public void Source_LinksOnlyWhileTheTrackExists()
    {
        var page = RenderAt(ExperienceLevel.Advanced);

        var available = page.Find($"[data-testid=asset-source-{AssetData.AssetId}]");
        (available.TagName, available.GetAttribute("data-track-available"), available.GetAttribute("href")).ShouldBe(("A", "true", $"tracks/{TestData.TrackId}"));
        var deleted = page.Find($"[data-testid=asset-source-{ElsewhereId}]");
        (deleted.TagName, deleted.GetAttribute("data-track-available")).ShouldBe(("SPAN", "false"));
        deleted.TextContent.ShouldBe("Hero of the Day (deleted)");
    }

    [Fact]
    public void GainNote_ExistsOnlyWhenTheGainWasLowered()
    {
        var page = RenderAt(ExperienceLevel.Advanced);

        var note = page.Find($"[data-testid=asset-gain-note-{DrumsId}]");
        note.TextContent.ShouldBe("Lowered by 1.8 dB to avoid clipping");
        note.GetAttribute("data-help-id").ShouldBe("export-clipping");
        page.FindAll($"[data-testid=asset-gain-note-{AssetData.AssetId}]").ShouldBeEmpty();
    }

    [Fact]
    public async Task Rename_ValidatesAndSavesTheTrimmedName()
    {
        Api.RenameAssetAsync(AssetData.AssetId, "Bass line", Arg.Any<CancellationToken>()).Returns(Bass with { Name = "Bass line" });
        var page = RenderAt(ExperienceLevel.Advanced);
        await page.Find($"[data-testid=asset-rename-{AssetData.AssetId}]").ClickAsync(new());
        page.Find("[data-testid=asset-rename-input]").GetAttribute("value").ShouldBe(Bass.Name);

        page.Find("[data-testid=asset-rename-input]").Input("   ");
        await page.Find("[data-testid=asset-rename-confirm]").ClickAsync(new());
        page.Find("[data-testid=asset-rename-error]").TextContent.ShouldBe("Enter a name of 1 to 120 characters.");
        page.Find("[data-testid=asset-rename-input]").Input("  Bass line ");
        await page.Find("[data-testid=asset-rename-confirm]").ClickAsync(new());

        page.Find($"[data-testid=asset-name-{AssetData.AssetId}]").TextContent.ShouldBe("Bass line");
        page.FindAll("[data-testid=asset-rename-input]").ShouldBeEmpty();
    }

    [Fact]
    public async Task Rename_RefusedByTheApi_StaysOpenWithTheReason()
    {
        Api.RenameAssetAsync(AssetData.AssetId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<AssetDto>(_ => throw Problem(HttpStatusCode.NotFound, "Not found", "The item no longer exists."));
        var page = RenderAt(ExperienceLevel.Advanced);
        await page.Find($"[data-testid=asset-rename-{AssetData.AssetId}]").ClickAsync(new());

        await page.Find("[data-testid=asset-rename-confirm]").ClickAsync(new());

        page.Find("[data-testid=asset-rename-error]").TextContent.ShouldBe("The item no longer exists.");
        await page.Find("[data-testid=asset-rename-cancel]").ClickAsync(new());
        page.FindAll("[data-testid=asset-rename-input]").ShouldBeEmpty();
    }

    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public async Task Delete_AsksFirst_AndRemovesTheRow(bool confirmed, int deletes)
    {
        Confirm.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(confirmed);
        var page = RenderAt(ExperienceLevel.Advanced);

        await page.Find($"[data-testid=asset-delete-{DrumsId}]").ClickAsync(new());

        await Confirm.Received(1).ConfirmAsync("Delete item", Arg.Is<string>(m => m.Contains("Drums for practice", StringComparison.Ordinal)), "Delete");
        await Api.Received(deletes).DeleteAssetAsync(DrumsId, Arg.Any<CancellationToken>());
        (page.FindAll($"[data-testid=asset-row-{DrumsId}]").Count == 0).ShouldBe(confirmed);
    }

    [Fact]
    public async Task DeleteFailure_ShowsTheReason()
    {
        Confirm.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        Api.DeleteAssetAsync(DrumsId, Arg.Any<CancellationToken>()).Returns(_ => throw Problem(HttpStatusCode.ServiceUnavailable, "Down", "The API is down."));
        var page = RenderAt(ExperienceLevel.Advanced);

        await page.Find($"[data-testid=asset-delete-{DrumsId}]").ClickAsync(new());

        page.Find("[data-testid=library-action-error]").TextContent.ShouldBe("The API is down.");
    }

    [Fact]
    public async Task Failed_ShowsTheReasonAndARetry_WhichQueuesItAgain()
    {
        var failed = AssetData.Asset(status: AssetStatusDto.Failed);
        Api.RetryAssetAsync(AssetData.AssetId, Arg.Any<CancellationToken>()).Returns(AssetData.Asset(status: AssetStatusDto.Queued));
        var page = RenderAt(ExperienceLevel.Advanced, failed, Drums);
        page.Find($"[data-testid=asset-error-{AssetData.AssetId}]").TextContent.ShouldBe(failed.Error);
        page.FindAll($"[data-testid=asset-retry-{DrumsId}], [data-testid=asset-error-{DrumsId}]").ShouldBeEmpty();

        await page.Find($"[data-testid=asset-retry-{AssetData.AssetId}]").ClickAsync(new());

        page.Find($"[data-testid=asset-row-{AssetData.AssetId}]").GetAttribute("data-status").ShouldBe("queued");
        page.FindAll($"[data-testid=asset-retry-{AssetData.AssetId}]").ShouldBeEmpty();
    }

    [Fact]
    public async Task RetryRefused_ShowsTheConflict()
    {
        Api.RetryAssetAsync(AssetData.AssetId, Arg.Any<CancellationToken>())
            .Returns<AssetDto>(_ => throw Problem(HttpStatusCode.Conflict, "Source unavailable", "The track was deleted."));
        var page = RenderAt(ExperienceLevel.Advanced, AssetData.Asset(status: AssetStatusDto.Failed));

        await page.Find($"[data-testid=asset-retry-{AssetData.AssetId}]").ClickAsync(new());

        page.Find("[data-testid=library-action-error]").TextContent.ShouldBe("The track was deleted.");
    }
}
