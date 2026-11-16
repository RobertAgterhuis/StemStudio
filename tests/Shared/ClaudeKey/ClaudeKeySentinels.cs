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

    /// <summary>The forms a leak search looks for: the value, its UTF-8 base64 and its UTF-16 (little endian) bytes as base64.</summary>
    /// <param name="sentinel">A sentinel of this class.</param>
    /// <returns>The searched forms.</returns>
    public static IReadOnlyList<string> SearchForms(string sentinel) =>
    [
        sentinel,
        Convert.ToBase64String(Encoding.UTF8.GetBytes(sentinel)),
        Convert.ToBase64String(Encoding.Unicode.GetBytes(sentinel)),
    ];
}
