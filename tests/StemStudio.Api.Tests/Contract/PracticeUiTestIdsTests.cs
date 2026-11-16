using System.Text.RegularExpressions;

namespace StemStudio.Api.Tests.Contract;

/// <summary>
/// Merge rule of the UI test ids (architect, 012 Phase 0 T003): the full <c>ui-test-ids.md</c> 1.11.0 is the 1.21.0 document of
/// the 011 follow-up unchanged (title and the inserted changelog line aside) plus every id row of the 012
/// <c>ui-test-ids.delta.md</c>; nothing is renamed or removed.
/// </summary>
public sealed partial class PracticeUiTestIdsTests
{
    private static readonly Lazy<string[]> Base = new(() => Lines("011-claude-key", "ui-test-ids.md"));

    private static readonly Lazy<string[]> Merged = new(() => Lines("012", "ui-test-ids.md"));

    private static readonly Lazy<string[]> Delta = new(() => Lines("012", "ui-test-ids.delta.md"));

    [Fact]
    public void Merged_IsVersion1110()
    {
        (Base.Value[0], Merged.Value[0]).ShouldBe(("# Contract: UI test ids (v1.21.0)", "# Contract: UI test ids (v1.11.0)"));
    }

    [Fact]
    public void Merged_Contains1210LinesUnchangedAndInOrder()
    {
        var body = Base.Value.Skip(1).ToList();

        Merged.Value.Skip(1).Where(line => !IsAdded(line)).Take(body.Count).ShouldBe(body);
    }

    [Fact]
    public void Merged_ContainsEveryDeltaIdRowAndShortcut()
    {
        var rows = Delta.Value.Where(line => line.StartsWith("| Mixer", StringComparison.Ordinal) || line.StartsWith("| Export dialog", StringComparison.Ordinal)).ToList();
        var shortcuts = Delta.Value.Where(line => line.StartsWith("| `", StringComparison.Ordinal)).ToList();

        (rows.Count, shortcuts.Count).ShouldBe((26, 7));
        rows.Concat(shortcuts).ShouldAllBe(row => Merged.Value.Contains(row));
    }

    [Fact]
    public void Merged_IdsAreNewAndUnique()
    {
        var known = Base.Value.Where(line => line.StartsWith('|')).SelectMany(line => line.Split('`')).ToHashSet(StringComparer.Ordinal);
        var added = Ids(Delta.Value).ToList();

        (added.Count, added.Distinct().Count()).ShouldBe((26, 26));
        added.Where(known.Contains).ShouldBeEmpty();
        added.ShouldContain("export-practice-note");
    }

    [Fact]
    public void Merged_RecordsTheChangelogLine()
    {
        Merged.Value.ShouldContain(line => line.StartsWith("1.11.0 (additive, feature 012", StringComparison.Ordinal));
    }

    private static bool IsAdded(string line) => !Base.Value.Contains(line);

    // The id a table row defines: the second column of the 012 rows (`| Screen | `id` | ...`).
    private static IEnumerable<string> Ids(IEnumerable<string> lines) =>
        lines.Select(line => IdPattern().Match(line)).Where(match => match.Success).Select(match => match.Groups[1].Value);

    private static string[] Lines(string folder, string file) =>
        File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Contract", folder, file));

    [GeneratedRegex(@"^\| (?:Mixer[^|]*|Export dialog) \| `((?:practice|countin|trainer|export-practice)[a-z-]*)`")]
    private static partial Regex IdPattern();
}
