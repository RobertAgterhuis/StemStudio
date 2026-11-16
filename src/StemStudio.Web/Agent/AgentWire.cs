using System.Text.Json;

namespace StemStudio.Web.Agent;

/// <summary>Wire names of contract enums for <c>data-*</c> attributes (ui-test-ids 1.10.0 uses the JSON values).</summary>
public static class AgentWire
{
    /// <summary>The JSON value of an enum member, e.g. <c>waiting_job</c>.</summary>
    /// <typeparam name="T">A contract enum with a string converter.</typeparam>
    /// <param name="value">The member.</param>
    /// <returns>The wire name.</returns>
    public static string Name<T>(T value)
        where T : struct, Enum => JsonSerializer.Serialize(value, AgentApiClient.Json).Trim('"');

    /// <summary>
    /// Enter sends, Shift+Enter keeps a new line (ui-test-ids <c>agent-input</c>): when Enter without Shift was pressed
    /// and the browser inserted exactly one new line, returns the text without it.
    /// </summary>
    /// <param name="previous">The text before the input event.</param>
    /// <param name="current">The text after it.</param>
    /// <param name="text">The text without the inserted new line.</param>
    /// <returns>True when the input was the Enter key.</returns>
    public static bool TryStripEnter(string previous, string current, out string text)
    {
        var index = InsertedAt(previous, current);
        var inserted = current.Length == previous.Length + 1 && current[index] == '\n';
        text = inserted ? current.Remove(index, 1) : current;
        return inserted;
    }

    private static int InsertedAt(string previous, string current)
    {
        var index = 0;
        while (index < previous.Length && index < current.Length && previous[index] == current[index])
        {
            index++;
        }

        return Math.Min(index, Math.Max(current.Length - 1, 0));
    }
}
