using System.Globalization;
using System.Text.RegularExpressions;
using Bunit;
using Microsoft.JSInterop;
using NSubstitute;
using StemStudio.Web.Audio;
using StemStudio.Web.Preferences;
using StemStudio.Web.Tests.Design;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Mixer.Karaoke;

/// <summary>Banner controls (006 T053, FR-009, FR-011): show/hide remembered in local storage, text size, reduced motion, contrast.</summary>
public sealed partial class KaraokeControlsTests : KaraokeTestContext
{
    private const string Key = "stemstudio.karaoke";

    [Fact]
    public async Task Toggle_HidesTheBanner_DetachesIt_AndRemembersTheChoice()
    {
        var page = RenderBanner(LyricsData.Found());
        page.Find("[data-testid=karaoke-toggle]").GetAttribute("aria-pressed").ShouldBe("true");

        await page.Find("[data-testid=karaoke-toggle]").ClickAsync(new());

        page.Find("[data-testid=karaoke-toggle]").GetAttribute("aria-pressed").ShouldBe("false");
        page.FindAll("[data-testid=karaoke-banner]").ShouldBeEmpty();
        page.FindAll("[data-testid=karaoke-unavailable]").ShouldBeEmpty();
        page.FindAll("[data-testid=karaoke-offset]").ShouldBeEmpty();
        page.WaitForAssertion(() => Engine.Received(1).DetachKaraokeAsync());
        JSInterop.VerifyInvoke("localStorage.setItem").Arguments.ShouldBe([Key, "{\"visible\":false,\"size\":\"m\"}"]);
    }

    [Fact]
    public async Task ShowingAgain_AttachesAFreshBanner()
    {
        var page = RenderBanner(LyricsData.Found());
        await page.Find("[data-testid=karaoke-toggle]").ClickAsync(new());

        await page.Find("[data-testid=karaoke-toggle]").ClickAsync(new());

        page.Find("[data-testid=karaoke-banner]").ShouldNotBeNull();
        page.WaitForAssertion(() => Engine.Received(2).AttachKaraokeAsync("karaoke-host", Arg.Any<KaraokeLyrics>(), Arg.Any<KaraokeOptions>()));
    }

    [Fact]
    public void HiddenBanner_IsRememberedAcrossVisits()
    {
        JSInterop.Setup<string?>("localStorage.getItem", Key).SetResult("{\"visible\":false,\"size\":\"l\"}");

        var page = RenderBanner(LyricsData.Found());

        page.Find("[data-testid=karaoke-toggle]").GetAttribute("aria-pressed").ShouldBe("false");
        page.FindAll("[data-testid=karaoke-banner]").ShouldBeEmpty();
        Engine.DidNotReceiveWithAnyArgs().AttachKaraokeAsync(default!, default!, default!);
    }

    [Fact]
    public void HiddenBanner_AlsoHidesTheUnavailableNotice()
    {
        JSInterop.Setup<string?>("localStorage.getItem", Key).SetResult("{\"visible\":false}");

        var page = RenderBanner(LyricsData.Empty(StemStudio.Contracts.Lyrics.LyricsStatusDto.NotFound));

        page.FindAll("[data-testid=karaoke-unavailable]").ShouldBeEmpty();
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"visible\":true,\"size\":\"xxl\"}")]
    [InlineData("null")]
    public void UnreadableStoredChoice_FallsBackToTheDefaults(string stored)
    {
        JSInterop.Setup<string?>("localStorage.getItem", Key).SetResult(stored);

        var page = RenderBanner(LyricsData.Found());

        page.Find("[data-testid=karaoke-banner]").GetAttribute("data-size").ShouldBe("m");
    }

    [Fact]
    public async Task BlockedStorage_KeepsTheBannerWorking()
    {
        JSInterop.Setup<string?>("localStorage.getItem", Key).SetException(new JSException("SecurityError: storage is disabled"));
        JSInterop.SetupVoid("localStorage.setItem", _ => true).SetException(new JSException("QuotaExceededError"));
        var page = RenderBanner(LyricsData.Found());
        page.Find("[data-testid=karaoke-banner]").ShouldNotBeNull();

        await page.Find("[data-testid=karaoke-toggle]").ClickAsync(new());

        page.FindAll("[data-testid=karaoke-banner]").ShouldBeEmpty();
    }

    [Theory]
    [InlineData("s")]
    [InlineData("l")]
    public async Task Size_IsAppliedToTheHost_AndRemembered(string size)
    {
        var page = RenderBanner(LyricsData.Found());
        page.Find("[data-testid=karaoke-banner]").GetAttribute("data-size").ShouldBe("m");
        page.FindAll("[data-testid=karaoke-size] option").Select(o => o.GetAttribute("value")).ShouldBe(["s", "m", "l"]);

        await page.Find("[data-testid=karaoke-size]").ChangeAsync(new() { Value = size });

        page.Find("[data-testid=karaoke-banner]").GetAttribute("data-size").ShouldBe(size);
        JSInterop.VerifyInvoke("localStorage.setItem").Arguments[1].ShouldBe($"{{\"visible\":true,\"size\":\"{size}\"}}");
        await Engine.Received(1).AttachKaraokeAsync("karaoke-host", Arg.Any<KaraokeLyrics>(), Arg.Any<KaraokeOptions>());
    }

    [Fact]
    public async Task UnknownSize_IsIgnored()
    {
        var page = RenderBanner(LyricsData.Found());

        await page.Find("[data-testid=karaoke-size]").ChangeAsync(new() { Value = "huge" });

        page.Find("[data-testid=karaoke-banner]").GetAttribute("data-size").ShouldBe("m");
    }

    [Fact]
    public async Task AppReducedMotion_IsPassedToTheEngine()
    {
        await Preferences.SetMotionAsync(MotionPreference.Reduced);

        RenderBanner(LyricsData.Found());

        AttachedOptions().ReducedMotion.ShouldBe(true);
    }

    [Fact]
    public void SystemMotion_LeavesTheChoiceToTheEngine()
    {
        RenderBanner(LyricsData.Found());

        AttachedOptions().ReducedMotion.ShouldBeNull();
    }

    [Theory]
    [InlineData("dark")]
    [InlineData("light")]
    public void BannerText_MeetsAAContrastOnTheBannerInBothThemes(string theme)
    {
        var css = RepoPaths.ReadWeb("wwwroot/css/components/karaoke.css");
        var tokens = ThemeTokens(theme);
        var host = Rule(css, ".ss-karaoke-host {");
        Regex.Match(host, @"background: var\(--ss-([a-z-]+)\)").Groups[1].Value.ShouldBe("color-raised");

        var colours = Colour().Matches(css).Select(m => m.Groups[1].Value).Distinct().ToList();
        colours.ShouldBeSubsetOf(["color-ink", "color-muted", "stem-vocals", "color-danger", "forced-accent"]);
        foreach (var colour in new[] { "color-ink", "color-muted", "stem-vocals" })
        {
            var ratio = TokenContrastTests.Ratio(Resolve(tokens, colour), Resolve(tokens, "color-raised"));
            ratio.ShouldBeGreaterThanOrEqualTo(4.5, $"{theme}: {colour} on the banner is {ratio.ToString("0.00", CultureInfo.InvariantCulture)}:1");
        }
    }

    [GeneratedRegex(@"(?<![a-z-])color: var\(--ss-([a-z-]+)\)")]
    private static partial Regex Colour();

    private static string Rule(string css, string selector)
    {
        var start = css.IndexOf(selector, StringComparison.Ordinal);
        start.ShouldBeGreaterThan(0);
        return css[start..css.IndexOf('}', start)];
    }

    private static Dictionary<string, string> ThemeTokens(string theme)
    {
        var block = Rule(RepoPaths.ReadWeb("wwwroot/css/tokens.css"), $"html[data-theme=\"{theme}\"] {{");
        return Regex.Matches(block, @"--ss-([a-z0-9-]+)\s*:\s*([^;]+);").ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value.Trim(), StringComparer.Ordinal);
    }

    private static string Resolve(Dictionary<string, string> tokens, string name) =>
        Regex.Match(tokens[name], @"^var\(--ss-([a-z0-9-]+)\)$") is { Success: true } reference ? Resolve(tokens, reference.Groups[1].Value) : tokens[name];
}
