namespace StemStudio.Web.Help;

/// <summary>One heading of a rendered topic.</summary>
/// <param name="Id">Auto-generated heading id (anchor).</param>
/// <param name="Text">Heading text.</param>
/// <param name="Level">Heading level, 2 or higher.</param>
public sealed record HelpHeading(string Id, string Text, int Level);
