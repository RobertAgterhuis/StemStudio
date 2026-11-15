namespace StemStudio.Web.Help;

/// <summary>One help source file (contracts/help-content.md 1).</summary>
/// <param name="Path">Path below <c>Help/</c>: <c>topics/&lt;area&gt;/&lt;id&gt;.md</c> or <c>tours/&lt;id&gt;.json</c>.</param>
/// <param name="Content">The file text.</param>
public sealed record HelpFile(string Path, string Content);

/// <summary>Help content that violates contracts/help-content.md; fails start-up and the help tests.</summary>
/// <param name="message">What is wrong, starting with the file.</param>
public sealed class HelpContentException(string message) : Exception(message)
{
    /// <summary>Creates the exception for one file.</summary>
    /// <param name="path">The help file path.</param>
    /// <param name="problem">The problem.</param>
    /// <returns>The exception.</returns>
    internal static HelpContentException For(string path, string problem) => new($"Help file '{path}': {problem}.");
}
