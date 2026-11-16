using Microsoft.Extensions.Logging;
using StemStudio.TestDoubles.ClaudeKey;

namespace StemStudio.Api.Tests.TestSupport;

/// <summary>
/// Leak assertions of the 011 follow-up claude-key [sec] tests: every <see cref="ClaudeKeySentinels.SearchForms"/> of every given
/// sentinel is absent. Failure messages name the place, never the value.
/// </summary>
internal static class KeyLeaks
{
    /// <summary>Asserts that a text holds no search form of the sentinels.</summary>
    /// <param name="text">The searched text.</param>
    /// <param name="where">The place, for the failure message.</param>
    /// <param name="sentinels">The sentinels.</param>
    public static void ShouldNotContainAny(string text, string where, params string[] sentinels)
    {
        foreach (var form in sentinels.SelectMany(ClaudeKeySentinels.SearchForms))
        {
            text.Contains(form, StringComparison.Ordinal).ShouldBeFalse($"a search form of a sentinel was found in {where}");
        }
    }

    /// <summary>
    /// Asserts that the capture really ran at Trace (positive control, qa F1) and that no log message, exception or scope holds a
    /// search form of the sentinels.
    /// </summary>
    /// <param name="logs">The captured logs.</param>
    /// <param name="sentinels">The sentinels.</param>
    public static void TraceLogsShouldBeClean(ListLoggerProvider logs, params string[] sentinels)
    {
        logs.Entries.ShouldContain(entry => entry.Level == LogLevel.Trace, "the Trace filter did not apply");
        foreach (var line in logs.Entries.Select(entry => entry.Message + entry.Exception).Concat(logs.Scopes))
        {
            ShouldNotContainAny(line, "a log line or scope", sentinels);
        }
    }
}
