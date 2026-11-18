namespace StemStudio.Web.Tests.Fakes;

/// <summary>Locates source files of the web project from the test output directory.</summary>
public static class RepoPaths
{
    /// <summary>The repository root (the folder holding <c>StemStudio.slnx</c>).</summary>
    public static string Root { get; } = FindRoot();

    /// <summary>The <c>src/StemStudio.Web</c> folder.</summary>
    public static string Web => Path.Combine(Root, "src", "StemStudio.Web");

    /// <summary>Reads a file below <c>src/StemStudio.Web</c>.</summary>
    /// <param name="relative">Path relative to the web project, with forward slashes.</param>
    /// <returns>The file text.</returns>
    public static string ReadWeb(string relative) => File.ReadAllText(Path.Combine(Web, relative));

    /// <summary>The first CSS rule that starts with <paramref name="selector"/>, up to its closing brace.</summary>
    /// <param name="css">The style sheet text.</param>
    /// <param name="selector">The selector text including the opening brace, e.g. <c>.ss-x {</c>.</param>
    /// <returns>The rule text without the closing brace.</returns>
    public static string CssRule(string css, string selector)
    {
        var start = css.IndexOf(selector, StringComparison.Ordinal);
        start.ShouldBeGreaterThanOrEqualTo(0, selector);
        return css[start..css.IndexOf('}', start)];
    }

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "StemStudio.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new DirectoryNotFoundException("StemStudio.slnx not found above the test output.");
    }
}
