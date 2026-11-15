namespace StemStudio.Web.Help;

/// <summary>One step of a tour (contracts/help-content.md 3).</summary>
/// <param name="Target"><c>data-help-id</c> of the element the step points at.</param>
/// <param name="Topic">Topic whose title and summary are shown unless overridden.</param>
/// <param name="Title">Optional title override (1-60 characters).</param>
/// <param name="Text">Optional text override (1-200 characters).</param>
public sealed record TourStep(string Target, string Topic, string? Title = null, string? Text = null);
