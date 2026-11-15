using Bunit;
using StemStudio.Contracts.Lyrics;
using StemStudio.Web.Help;
using StemStudio.Web.Tests.Fakes;
using StemStudio.Web.Tests.Pages.Lyrics;

namespace StemStudio.Web.Tests.Mixer.Karaoke;

/// <summary>Accessibility of the banner and the lyrics card (006 T060, WCAG 2.2 AA): text alternative, names, order.</summary>
public sealed class KaraokeAccessibilityTests : KaraokeTestContext
{
    private static readonly HelpCatalog Catalog = HelpCatalog.FromAssembly(typeof(HelpCatalog).Assembly);

    [Fact]
    public void Banner_IsNotLive_AndOffersTheFullLyricsAsText()
    {
        var page = RenderBanner(LyricsData.Found());
        page.Find("[data-testid=karaoke-banner]").GetAttribute("aria-live").ShouldBe("off");
        var toggle = page.Find("[data-testid=karaoke-full-lyrics]");
        toggle.GetAttribute("aria-expanded").ShouldBe("false");
        page.FindAll("[data-testid=karaoke-full-lyrics-text]").ShouldBeEmpty();

        toggle.Click();

        var list = page.Find("[data-testid=karaoke-full-lyrics-text]");
        page.Find("[data-testid=karaoke-full-lyrics]").GetAttribute("aria-expanded").ShouldBe("true");
        page.Find("[data-testid=karaoke-full-lyrics]").GetAttribute("aria-controls").ShouldBe(list.Id);
        list.QuerySelectorAll("li").Select(li => li.TextContent).ShouldBe(["Fake line one", "La la la"]);
        page.Find("[data-testid=karaoke-full-lyrics]").Click();
        page.FindAll("[data-testid=karaoke-full-lyrics-text]").ShouldBeEmpty();
    }

    [Fact]
    public void EveryBannerControl_HasAName_AndComesBeforeTheBannerInTheTabOrder()
    {
        var page = RenderBanner(LyricsData.Found());

        var controls = page.FindAll(".ss-karaoke input, .ss-karaoke button, .ss-karaoke select");
        controls.Select(c => c.GetAttribute("data-testid")).Where(id => id is not null)
            .ShouldBe(["karaoke-toggle", "karaoke-size", "karaoke-offset", "karaoke-offset-reset", "karaoke-full-lyrics"]);
        foreach (var control in controls)
        {
            (control.GetAttribute("aria-label") ?? control.TextContent.Trim()).ShouldNotBeNullOrWhiteSpace(control.OuterHtml);
            control.GetAttribute("tabindex").ShouldNotBe("-1");
        }

        var html = page.Markup;
        html.IndexOf("karaoke-full-lyrics", StringComparison.Ordinal).ShouldBeLessThan(html.IndexOf("karaoke-host", StringComparison.Ordinal));
    }

    [Fact]
    public void UnavailableNotice_IsAPoliteStatus() =>
        RenderBanner(LyricsData.Empty(LyricsStatusDto.Searching)).Find("[data-testid=karaoke-unavailable]").GetAttribute("role").ShouldBe("status");

    [Fact]
    public void KaraokeHelp_MentionsTheTextAlternativeAndTheKeys()
    {
        var body = Catalog.Topic("karaoke")!.Markdown;

        body.ShouldContain("Show full lyrics");
        body.ShouldContain("screen reader");
        Catalog.Topic("karaoke-offset")!.Markdown.ShouldContain("arrow keys");
    }
}

/// <summary>Accessibility of the lyrics card (006 T060).</summary>
public sealed class LyricsCardAccessibilityTests : LyricsTestContext
{
    [Fact]
    public async Task PasteInput_IsLabelled_AndEveryCardControlIsNamed()
    {
        var page = RenderSection(LyricsData.Plain());
        await page.Find("[data-testid=lyrics-paste-open]").ClickAsync(new());

        var input = page.Find("[data-testid=lyrics-paste-input]");
        page.Find($"label[for={input.Id}]").TextContent.ShouldContain("Your lyrics");
        foreach (var control in page.FindAll("[data-testid=lyrics-section] button"))
        {
            (control.GetAttribute("aria-label") ?? control.TextContent.Trim()).ShouldNotBeNullOrWhiteSpace(control.OuterHtml);
        }
    }

    [Fact]
    public void Section_IsLabelledByItsHeading()
    {
        var page = RenderSection(LyricsData.Found());

        var section = page.Find("[data-testid=lyrics-section]");
        page.Find($"#{section.GetAttribute("aria-labelledby")}").TextContent.ShouldBe("Lyrics");
    }

    [Fact]
    public void Preview_IsANamedRegion()
    {
        var preview = RenderSection(LyricsData.Plain()).Find("[data-testid=lyrics-preview]");

        preview.GetAttribute("role").ShouldBe("region");
        preview.GetAttribute("aria-label").ShouldBe("Lyrics without timing");
    }
}
