namespace StemStudio.Web.Help;

/// <summary>In-memory full-text search over the help topics (research.md R-06).</summary>
public interface IHelpSearch
{
    /// <summary>Searches titles, summaries, keywords and bodies.</summary>
    /// <param name="query">User query; blank returns no hits.</param>
    /// <param name="max">Maximum number of hits.</param>
    /// <returns>Hits ordered by descending score, then by title.</returns>
    IReadOnlyList<HelpHit> Search(string query, int max = 10);
}
