using System.Text.RegularExpressions;

namespace StemStudio.Api.Tests.Contract;

/// <summary>
/// Merge rule of the UI test ids (architect, 023 Phase 0 T003): the full <c>ui-test-ids.md</c> 1.23.0 is 012's 1.11.0 unchanged
/// (title and the inserted changelog line aside) plus every row of the 023 <c>ui-test-ids.delta.md</c>; nothing is renamed or removed.
/// </summary>
public sealed partial class LyricAssistUiTestIdsTests
{
    private const int DeltaRows = 48;

    private static readonly Lazy<string[]> Base = new(() => Lines("012", "ui-test-ids.md"));

    private static readonly Lazy<string[]> Merged = new(() => Lines("023", "ui-test-ids.md"));

    private static readonly Lazy<string[]> Delta = new(() => Lines("023", "ui-test-ids.delta.md"));

    [Fact]
    public void Merged_IsVersion1230()
    {
        (Base.Value[0], Merged.Value[0]).ShouldBe(("# Contract: UI test ids (v1.11.0)", "# Contract: UI test ids (v1.23.0)"));
    }

    [Fact]
    public void Merged_Contains1110LinesUnchangedAndInOrder()
    {
        var body = Base.Value.Skip(1).ToList();

        Merged.Value.Skip(1).Where(line => Base.Value.Contains(line)).Take(body.Count).ShouldBe(body);
        Merged.Value.ShouldContain(line => line.StartsWith("1.23.0 (additive, feature 023", StringComparison.Ordinal));
    }

    [Fact]
    public void Merged_ContainsEveryDeltaRowAndKey()
    {
        var rows = Delta.Value.Where(line => RowPattern().IsMatch(line)).ToList();
        var keys = Delta.Value.Where(line => line.StartsWith("| F8", StringComparison.Ordinal) || line.StartsWith("| Alt+", StringComparison.Ordinal)
            || line.StartsWith("| Escape", StringComparison.Ordinal) || line.StartsWith("| Ctrl+Alt+C", StringComparison.Ordinal)).ToList();

        (rows.Count, keys.Count).ShouldBe((DeltaRows, 6));
        rows.Concat(keys).ShouldAllBe(row => Merged.Value.Contains(row));
    }

    [Fact]
    public void Merged_IdsAreNewAndUnique()
    {
        var firstIds = DeltaIds();
        var baseText = string.Join(Environment.NewLine, Base.Value);

        firstIds.ShouldBeUnique();
        firstIds.Count.ShouldBe(DeltaRows);
        firstIds.ShouldAllBe(id => !baseText.Contains($"`{id}`", StringComparison.Ordinal));
        firstIds.ShouldContain("lyric-assist-pane");
        firstIds.ShouldContain("take-lyrics-assisted");
    }

    [Fact]
    public void Merged_ListsTheSixHelpTopics()
    {
        var topics = new[] { "lyric-assist", "lyric-assist-licence", "lyric-assist-devices", "lyric-assist-rhythm", "lyric-assist-privacy", "lyric-assist-limits" };
        var text = string.Join('\n', Merged.Value.SkipWhile(line => !line.StartsWith("### Help topics", StringComparison.Ordinal)));

        topics.ShouldAllBe(topic => text.Contains($"`{topic}`", StringComparison.Ordinal));
    }

    private static List<string> DeltaIds() =>
        [.. Delta.Value.Select(line => RowPattern().Match(line)).Where(match => match.Success).Select(match => match.Groups[1].Value)];

    private static string[] Lines(string folder, string file) =>
        File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Contract", folder, file));

    // A delta row: `| Lyrics editor | `lyric-...` | ...`, `| Take details (010) | ...`, `| Agent panel (011) | ...`; group 1 = the first id.
    [GeneratedRegex(@"^\| (?:Lyrics editor|Take details \(010\)|Agent panel \(011\)) \| `([A-Za-z0-9<>-]+)`")]
    private static partial Regex RowPattern();
}
