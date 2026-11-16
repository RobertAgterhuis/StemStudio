using System.Text;

namespace StemStudio.TestDoubles.ClaudeKey;

/// <summary>
/// Well-formed sentinel Claude keys for tests (011 follow-up claude-key). Built at run time from parts: the repository secret
/// scan (<c>scripts/secret_scan.py</c>) fails on the key prefix followed by a key character in any file (research R-07), so no
/// test, fixture or snapshot may contain one as a literal. A sentinel must never appear in a response, log, event or database file.
/// Linked into the test projects as <c>Shared/ClaudeKey</c> (architect, T005).
/// </summary>
public static class ClaudeKeySentinels
{
    /// <summary>The key prefix, assembled so the source never holds it next to a key character.</summary>
    public static string Prefix { get; } = string.Concat("sk", "-", "ant", "-");

    /// <summary>Sentinel used as the <c>ANTHROPIC_API_KEY</c> environment value; ends with <c>ENV1</c>.</summary>
    public static string Environment { get; } = Build("env", "ENV1");

    /// <summary>Sentinel saved through the key endpoint; ends with <c>STO1</c>.</summary>
    public static string Stored { get; } = Build("stored", "STO1");

    /// <summary>A second stored sentinel for replace tests; ends with <c>STO2</c>.</summary>
    public static string Replacement { get; } = Build("replace", "STO2");

    /// <summary>An admin-shaped value that the format check must refuse (reason <c>admin-key</c>).</summary>
    public static string Admin { get; } = string.Concat(Prefix, "admin01-", new string('a', 60), "ADM1");

    /// <summary>A well-formed sentinel with a distinct marker and last four characters.</summary>
    /// <param name="marker">Lower-case letters that make the sentinel unique.</param>
    /// <param name="lastFour">Last four key characters (letters, digits, <c>-</c>, <c>_</c>).</param>
    /// <returns>A key-shaped string of 7 + 6 + marker + 10 + 38 + 4 characters (well inside 40-256).</returns>
    public static string Build(string marker, string lastFour) =>
        string.Concat(Prefix, "api03-", marker, "-sentinel-", new string('x', 38), lastFour);

    /// <summary>
    /// The forms a leak search looks for (quality-gate R1): the value; its distinctive middle (without the prefix and the last
    /// four characters, so a truncated or masked key is found); and the UTF-8 and UTF-16 (little endian) bytes as base64 in all
    /// three alignments, so the key is also found inside a larger base64 payload (for example a logged JSON body).
    /// </summary>
    /// <param name="sentinel">A sentinel of this class.</param>
    /// <returns>The searched forms (the plain value first).</returns>
    public static IReadOnlyList<string> SearchForms(string sentinel)
    {
        ArgumentNullException.ThrowIfNull(sentinel);
        string[] base64 = [.. new[] { Encoding.UTF8, Encoding.Unicode }.SelectMany(encoding => Base64Alignments(encoding.GetBytes(sentinel)))];
        return [sentinel, Middle(sentinel), .. base64.Distinct(StringComparer.Ordinal)];
    }

    // The characters of the base64 text that depend only on the sentinel's bytes, for a payload offset of 0, 1 and 2 bytes:
    // the first and the last group of four characters may mix in neighbouring bytes, so both are dropped.
    private static IEnumerable<string> Base64Alignments(byte[] bytes) =>
        Enumerable.Range(0, 3).Select(pad => Convert.ToBase64String([.. new byte[pad], .. bytes])[4..^4]);

    private static string Middle(string sentinel) =>
        sentinel.StartsWith(Prefix, StringComparison.Ordinal) ? sentinel[Prefix.Length..^4] : sentinel[..^4];
}
