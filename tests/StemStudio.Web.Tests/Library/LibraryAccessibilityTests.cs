using System.Text.RegularExpressions;
using Bunit;
using StemStudio.Contracts.Library;
using StemStudio.Web.Experience;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Library;

/// <summary>
/// WCAG 2.2 AA and polish of the 022 screens (022 T087, SC-008): unique ids, named controls, decorative pictures,
/// target size, focus not obscured, marks not by colour alone, 320 px layout, compact density and reduced motion.
/// </summary>
public sealed partial class LibraryAccessibilityTests : LibraryTestContext
{
    private static readonly string Css = Section(RepoPaths.ReadWeb("wwwroot/css/components/library.css"));

    private static string Section(string css) => css[css.IndexOf("Library folders, folder art and live search", StringComparison.Ordinal)..];

    public static TheoryData<string> Screens => new() { "root", "song", "search" };

    private IRenderedComponent<Microsoft.AspNetCore.Components.CascadingValue<ExperienceContext>> Screen(string screen, ExperienceLevel level)
    {
        Folder(LibraryData.SongId, LibraryData.SongView() with { Items = [LibraryData.Asset()] });
        SearchAnswer = () => LibraryData.EmptySearch() with
        {
            Artists = [new FolderHitDto { Folder = LibraryData.RootView().Children[0] }],
            Items = [new ItemHitDto { Asset = LibraryData.Asset() }],
            Totals = new SearchTotalsDto { Artists = 1, Albums = 0, Songs = 0, Items = 1 },
        };
        return screen switch
        {
            "song" => RenderFolder(LibraryData.SongId, level),
            "search" => RenderLibrary("library?q=met", level),
            _ => RenderLibrary(level: level),
        };
    }

    [Theory]
    [MemberData(nameof(Screens))]
    public async Task EveryScreen_HasUniqueIds_NamedButtons_AndDecorativePictures(string screen)
    {
        foreach (var level in new[] { ExperienceLevel.Beginner, ExperienceLevel.Advanced })
        {
            var page = Screen(screen, level);

            var ids = page.FindAll("[id]").Select(e => e.Id).ToList();
            ids.ShouldBeUnique($"{screen} {level}");
            page.FindAll("[data-testid]").Select(e => e.GetAttribute("data-testid")).Where(t => t != "search-mark").ShouldBeUnique();
            page.FindAll("button").ShouldAllBe(b => !string.IsNullOrWhiteSpace(b.GetAttribute("aria-label") ?? b.TextContent));
            page.FindAll("img").ShouldAllBe(img => img.GetAttribute("alt") == string.Empty && img.HasAttribute("width") && img.HasAttribute("height"));
            page.FindAll("input:not([type=checkbox])").ShouldAllBe(i => page.FindAll($"label[for={i.Id}]").Count == 1);
            page.FindAll("h1").Count.ShouldBe(1, screen);
            await DisposeComponentsAsync();
        }
    }

    [Fact]
    public void Buttons_WithAnAriaLabel_ContainTheirVisibleText()
    {
        Folder(LibraryData.SongId, LibraryData.SongView() with { Items = [LibraryData.Asset()] });
        var page = RenderFolder(LibraryData.SongId);

        foreach (var button in page.FindAll("button[aria-label]"))
        {
            var visible = (button.QuerySelector(".rz-button-text") ?? button).TextContent.Trim();
            button.GetAttribute("aria-label")!.ShouldContain(visible, Case.Insensitive, button.OuterHtml);
        }
    }

    [Theory]
    [InlineData(".ss-breadcrumb a")]
    [InlineData(".ss-library-switch")]
    [InlineData(".ss-search-hit > a")]
    public void SmallLinks_KeepTheMinimumTargetSize(string selector)
    {
        var rule = Rule(selector);

        rule.ShouldContain("min-height: var(--ss-target-min)");
    }

    [Fact]
    public void Marks_AreUnderlinedAndBold_NotColourAlone()
    {
        var rule = Rule(".ss-search-mark");

        rule.ShouldContain("text-decoration: underline");
        rule.ShouldContain("font-weight: 700");
    }

    [Fact]
    public void FocusedControls_StayAboveTheBottomBar_OnSmallScreens() =>
        Css.ShouldContain("html:has(.ss-library) {\n    scroll-padding-bottom: calc(var(--ss-transport-height) + var(--ss-space-2));");

    [Fact]
    public void At320Px_TheGridHasTwoShrinkableColumns_AndLongNamesWrap()
    {
        Css.ShouldContain("grid-template-columns: repeat(2, minmax(0, 1fr));");
        Rule(".ss-folder-name").ShouldContain("overflow-wrap: anywhere");
        Rule(".ss-folder-head h1").ShouldContain("overflow-wrap: anywhere");
        Rule(".ss-search-row .ss-search").ShouldContain("min-width: 0");
    }

    [Fact]
    public void Compact_Uses128PxTiles_AndTheArtHasAFixedRatio()
    {
        Css.ShouldContain("html[data-density=\"compact\"] .ss-library {\n  --ss-art-size: 128px;");
        Rule(".ss-folder-art").ShouldContain("aspect-ratio: 1");
    }

    [Fact]
    public void Tiles_HaveNoAnimation_SoReducedMotionHasNothingToStop()
    {
        Css.ShouldNotContain("transition");
        Css.ShouldNotContain("animation");
        Css.ShouldNotContain("transform");
    }

    [Fact]
    public void ArtTones_AreEightMixesOfThemeTokens()
    {
        for (var tone = 0; tone < StemStudio.Contracts.ContractLimits.ArtToneCount; tone++)
        {
            Css.ShouldContain($"--ss-art-tone-{tone}: color-mix(in oklab, ");
            Css.ShouldContain($".ss-art-tone-{tone} {{ --ss-art-tone: var(--ss-art-tone-{tone}); }}");
        }

        HexColour().IsMatch(Css).ShouldBeFalse();
    }

    [Fact]
    public void ItemTable_ShowsFocus_WhenArrowDownMovesThere() =>
        Rule(".ss-asset-table:focus-visible").ShouldContain("outline: 2px solid var(--ss-color-focus)");

    private static string Rule(string selector)
    {
        var start = Css.IndexOf('\n' + selector + " {", StringComparison.Ordinal);
        if (start < 0)
        {
            start = Css.IndexOf('\n' + selector + ",", StringComparison.Ordinal);
        }

        start.ShouldBeGreaterThanOrEqualTo(0, selector);
        var open = Css.IndexOf('{', start);
        return Css[open..Css.IndexOf('}', open)];
    }

    [GeneratedRegex("#[0-9a-fA-F]{3,8}\b")]
    private static partial Regex HexColour();
}
