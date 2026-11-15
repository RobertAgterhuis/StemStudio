using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StemStudio.Contracts.Assets;
using StemStudio.Web.Components.Library;
using StemStudio.Web.Help;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Library;

/// <summary>The library page (007 T039, FR-007, FR-008, ui-test-ids 1.5.0).</summary>
public sealed class LibraryPageTests : PageTestContext
{
    private static readonly Guid NewerId = Guid.Parse("7d4b2c8e-6a1f-4b3d-9e5c-2a3b4c5d6e7f");

    private IRenderedComponent<LibraryPage> RenderLibrary(params AssetListDto[] lists)
    {
        Api.ListAssetsAsync(Arg.Any<CancellationToken>()).Returns(lists[0], lists[1..]);
        return Render<LibraryPage>();
    }

    [Fact]
    public void Items_AreListedNewestFirst_WithNameContentsAndStatus()
    {
        var older = AssetData.Asset();
        var newer = AssetData.Asset(NewerId, name: "Practice backing track") with { CreatedAt = TestData.Created.AddHours(2) };

        var page = RenderLibrary(AssetData.List(older, newer));

        page.FindAll("[data-testid^=asset-row-]").Select(r => r.GetAttribute("data-testid")).ShouldBe([$"asset-row-{NewerId}", $"asset-row-{AssetData.AssetId}"]);
        page.Find($"[data-testid=asset-name-{AssetData.AssetId}]").TextContent.ShouldBe("Never Gonna Give You Up - bass");
        page.Find($"[data-testid=asset-contents-{AssetData.AssetId}]").TextContent.ShouldBe("bass, raw");
        page.Find($"[data-testid=asset-row-{AssetData.AssetId}]").GetAttribute("data-status").ShouldBe("ready");
        page.Find($"[data-testid=asset-status-{AssetData.AssetId}]").TextContent.ShouldContain("Ready");
        page.Find($"[data-testid=asset-row-{AssetData.AssetId}]").TextContent.ShouldContain("FLAC 16-bit");
        page.Find($"[data-testid=asset-row-{AssetData.AssetId}]").TextContent.ShouldContain("3:33");
    }

    [Fact]
    public void ReadyItem_HasAPlayerAndADownload()
    {
        var page = RenderLibrary(AssetData.List(AssetData.Asset()));

        var player = page.Find($"[data-testid=asset-play-{AssetData.AssetId}]");
        player.TagName.ShouldBe("AUDIO");
        player.GetAttribute("src").ShouldBe($"{ArtworkData.PublicBase}/api/assets/{AssetData.AssetId}/files/audio?inline=true");
        player.HasAttribute("controls").ShouldBeTrue();
        player.GetAttribute("preload").ShouldBe("none");
        player.GetAttribute("aria-label").ShouldBe("Play Never Gonna Give You Up - bass");
        var download = page.Find($"[data-testid=asset-download-{AssetData.AssetId}]");
        download.GetAttribute("href").ShouldBe($"{ArtworkData.PublicBase}/api/assets/{AssetData.AssetId}/files/audio");
        download.HasAttribute("download").ShouldBeTrue();
    }

    [Theory]
    [InlineData(AssetStatusDto.Queued)]
    [InlineData(AssetStatusDto.Rendering)]
    [InlineData(AssetStatusDto.Failed)]
    public void UnreadyItem_HasNoPlayerOrDownload(AssetStatusDto status)
    {
        var page = RenderLibrary(AssetData.List(AssetData.Asset(status: status)));

        page.FindAll($"[data-testid=asset-play-{AssetData.AssetId}]").ShouldBeEmpty();
        page.FindAll($"[data-testid=asset-download-{AssetData.AssetId}]").ShouldBeEmpty();
    }

    [Fact]
    public void Rendering_ShowsProgress_AndIsPolledUntilReady()
    {
        var page = RenderLibrary(AssetData.List(AssetData.Asset(status: AssetStatusDto.Rendering)), AssetData.List(AssetData.Asset()));
        var status = page.Find($"[data-testid=asset-status-{AssetData.AssetId}]");
        status.GetAttribute("role").ShouldBe("progressbar");
        status.GetAttribute("aria-valuenow").ShouldBe("42");
        status.TextContent.ShouldContain("Rendering 42 %");

        Timer.Tick();

        page.WaitForAssertion(() => page.Find($"[data-testid=asset-row-{AssetData.AssetId}]").GetAttribute("data-status").ShouldBe("ready"));
        page.Find($"[data-testid=asset-status-{AssetData.AssetId}]").HasAttribute("aria-valuenow").ShouldBeFalse();
        Timer.Waits.ShouldBe(1);
    }

    [Fact]
    public void Empty_ShowsTheEmptyStateAndTheTotalSize()
    {
        var page = RenderLibrary(AssetData.List());

        page.Find("[data-testid=library-empty]").TextContent.ShouldContain("No items yet");
        page.Find("[data-testid=library-total-size]").TextContent.ShouldBe("0 B");
        Timer.Waits.ShouldBe(0);
    }

    [Theory]
    [InlineData(0L, "0 B")]
    [InlineData(900L, "900 B")]
    [InlineData(1536L, "1.5 KB")]
    [InlineData(12_700_000L, "12 MB")]
    [InlineData(327_155_712L, "312 MB")]
    [InlineData(3L * 1024 * 1024 * 1024, "3.0 GB")]
    public void TotalSize_IsReadable(long bytes, string expected) =>
        StemStudio.Web.Services.ByteSize.Format(bytes).ShouldBe(expected);

    [Fact]
    public void TotalSize_ComesFromTheList()
    {
        var page = RenderLibrary(new AssetListDto([AssetData.Asset()], 1, 327_155_712));

        page.Find("[data-testid=library-total-size]").TextContent.ShouldBe("312 MB");
    }

    [Fact]
    public void LoadError_IsShown()
    {
        Api.ListAssetsAsync(Arg.Any<CancellationToken>()).Returns<AssetListDto>(_ => throw Problem(System.Net.HttpStatusCode.ServiceUnavailable, "Down"));

        var page = Render<LibraryPage>();

        page.Find("[data-testid=library-error]").TextContent.ShouldBe("Down");
    }

    [Fact]
    public void Header_CarriesTheLibraryHelpMarker_AndTheScreenTopicExists()
    {
        var page = RenderLibrary(AssetData.List());

        page.Find("h1").TextContent.ShouldBe("Library");
        page.Find("[data-help-id=library]").ShouldNotBeNull();
        HelpCatalog.FromAssembly(typeof(HelpCatalog).Assembly).Topic("library")!.Route.ShouldBe("/library");
    }

    [Fact]
    public void NavigationRail_LinksToTheLibrary()
    {
        var nav = Render<StemStudio.Web.Components.Layout.NavMenu>();

        nav.Find("[data-testid=nav-library]").GetAttribute("href").ShouldBe("library");
    }

    [Fact]
    public void HighlightedItem_FromTheConfirmationLink_IsMarked()
    {
        Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>().NavigateTo($"library?asset={AssetData.AssetId}");

        var page = RenderLibrary(AssetData.List(AssetData.Asset(), AssetData.Asset(NewerId)));

        page.Find($"[data-testid=asset-row-{AssetData.AssetId}]").GetAttribute("aria-current").ShouldBe("true");
        page.Find($"[data-testid=asset-row-{NewerId}]").HasAttribute("aria-current").ShouldBeFalse();
    }
}
