namespace StemStudio.Web.Help;

/// <summary>One search result (research.md R-06 scoring); higher scores rank first.</summary>
/// <param name="TopicId">Id of the matching topic.</param>
/// <param name="Score">Relevance score.</param>
public sealed record HelpHit(string TopicId, double Score);
