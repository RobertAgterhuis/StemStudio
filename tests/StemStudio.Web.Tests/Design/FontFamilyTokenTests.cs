using System.Text.RegularExpressions;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Design;

/// <summary>
/// 012 integration: <c>--ss-num</c> is a <c>font-variant-numeric</c> value (<c>tabular-nums</c>), not a font
/// stack. Used as <c>font-family</c> it names a font that does not exist and the browser falls back to its
/// default serif (the practice tempo values and the agent panel numbers rendered in Times).
/// </summary>
public sealed partial class FontFamilyTokenTests
{
    [Fact]
    public void NoStyleSheet_UsesTheNumericVariantToken_AsAFontFamily()
    {
        var offenders = Directory.EnumerateFiles(Path.Combine(RepoPaths.Web, "wwwroot", "css"), "*.css", SearchOption.AllDirectories)
            .SelectMany(file => FontFamilyNum().Matches(File.ReadAllText(file)).Select(_ => Path.GetFileName(file)))
            .ToList();

        offenders.ShouldBeEmpty();
    }

    [GeneratedRegex(@"font-family\s*:\s*var\(--ss-num\)")]
    private static partial Regex FontFamilyNum();
}
