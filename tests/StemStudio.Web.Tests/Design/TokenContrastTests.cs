using System.Globalization;
using System.Text.RegularExpressions;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Design;

/// <summary>WCAG contrast of the token pairs of design-spec.md 2.1 in both themes (SC-002, FR-030).</summary>
public sealed partial class TokenContrastTests
{
    private static readonly string Css = RepoPaths.ReadWeb("wwwroot/css/tokens.css");

    private static readonly string[] Stems = ["vocals", "drums", "bass", "guitar", "piano", "other"];
    private static readonly string[] Grounds = ["base", "surface", "raised", "sunken"];

    public static TheoryData<string> Themes => ["dark", "light"];

    [GeneratedRegex(@"(--ss-[a-z0-9-]+)\s*:\s*([^;]+);")]
    private static partial Regex Declaration();

    [GeneratedRegex(@"^var\((--ss-[a-z0-9-]+)\)$")]
    private static partial Regex Reference();

    private static Dictionary<string, string> Tokens(string theme)
    {
        var selector = theme == "dark" ? "html[data-theme=\"dark\"] {" : "html[data-theme=\"light\"] {";
        var start = Css.IndexOf(selector, StringComparison.Ordinal);
        start.ShouldBeGreaterThan(0, $"tokens.css has no {selector} block");
        var block = Css[start..Css.IndexOf('}', start)];
        return Declaration().Matches(block).ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value.Trim(), StringComparer.Ordinal);
    }

    private static string Hex(Dictionary<string, string> tokens, string name)
    {
        var value = tokens[$"--ss-{name}"];
        return Reference().Match(value) is { Success: true } reference ? Hex(tokens, reference.Groups[1].Value[5..]) : value;
    }

    private static double Luminance(string hex)
    {
        static double Channel(string hex, int index)
        {
            var c = int.Parse(hex.AsSpan(1 + (index * 2), 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Channel(hex, 0)) + (0.7152 * Channel(hex, 1)) + (0.0722 * Channel(hex, 2));
    }

    internal static double Ratio(string a, string b)
    {
        var (x, y) = (Luminance(a), Luminance(b));
        return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05);
    }

    private static void AssertPair(string theme, string fg, string bg, double minimum)
    {
        var tokens = Tokens(theme);
        var ratio = Ratio(Hex(tokens, fg), Hex(tokens, bg));
        ratio.ShouldBeGreaterThanOrEqualTo(minimum, $"{theme}: {fg} on {bg} is {ratio:0.00}:1");
    }

    [Fact]
    public void Ratio_MatchesTheWcagFormula()
    {
        Ratio("#000000", "#FFFFFF").ShouldBe(21, 0.01);
        Ratio("#777777", "#FFFFFF").ShouldBe(4.48, 0.01);
    }

    [Theory]
    [MemberData(nameof(Themes))]
    public void BodyText_IsAtLeast11point4_OnEverySurface(string theme)
    {
        foreach (var bg in Grounds)
        {
            AssertPair(theme, "color-ink", bg == "sunken" ? "color-sunken" : $"color-{bg}", 11.4);
        }
    }

    [Theory]
    [MemberData(nameof(Themes))]
    public void SecondaryText_MeetsAA_OnEverySurface(string theme)
    {
        foreach (var bg in Grounds)
        {
            AssertPair(theme, "color-muted", $"color-{bg}", 4.5);
        }
    }

    [Theory]
    [MemberData(nameof(Themes))]
    public void PrimaryButtonText_MeetsAA(string theme) => AssertPair(theme, "color-on-primary", "color-primary", 4.5);

    [Theory]
    [MemberData(nameof(Themes))]
    public void ControlBoundaries_AndFocus_AreAtLeast3_OnEverySurface(string theme)
    {
        foreach (var bg in Grounds)
        {
            AssertPair(theme, "color-control-border", $"color-{bg}", 3);
            AssertPair(theme, "color-focus", $"color-{bg}", 3);
        }
    }

    [Theory]
    [MemberData(nameof(Themes))]
    public void StateColours_AreReadableText_OnPanels_AndLabelsOnThemReadable(string theme)
    {
        foreach (var state in new[] { "success", "warning", "danger" })
        {
            AssertPair(theme, $"color-{state}", "color-surface", 4.5);
            AssertPair(theme, $"color-{state}", "color-raised", 4.5);
            AssertPair(theme, "color-on-state", $"color-{state}", 4.5);
            AssertPair(theme, $"color-{state}", "color-sunken", 3);
        }
    }

    [Theory]
    [MemberData(nameof(Themes))]
    public void StemColours_AreTextOnPanels_AndGraphicsOnTheBase(string theme)
    {
        foreach (var stem in Stems)
        {
            AssertPair(theme, $"stem-{stem}", "color-surface", 4.5);
            AssertPair(theme, $"stem-{stem}", "color-raised", 4.5);
            AssertPair(theme, $"stem-{stem}", "color-base", 4.0);
            AssertPair(theme, $"stem-{stem}", "color-sunken", 3);
        }
    }

    [Fact]
    public void OnlyTokensCss_DefinesLiteralColours_ForBothThemesTheSameTokenNames()
    {
        var dark = Tokens("dark").Keys.Order(StringComparer.Ordinal);
        var light = Tokens("light").Keys.Order(StringComparer.Ordinal);

        light.ShouldBe(dark);
    }
}
