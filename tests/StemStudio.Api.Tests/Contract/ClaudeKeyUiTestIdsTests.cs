using System.Text.RegularExpressions;

namespace StemStudio.Api.Tests.Contract;

/// <summary>
/// Merge rule of the UI test ids (architect, 011 follow-up claude-key T002): the full <c>ui-test-ids.md</c> 1.21.0 is 011's
/// 1.10.0 unchanged (title aside) plus every id row of <c>ui-test-ids.delta.md</c>; nothing is renamed or removed.
/// </summary>
public sealed partial class ClaudeKeyUiTestIdsTests
{
    private static readonly Lazy<string[]> Base = new(() => Lines("011", "ui-test-ids.md"));

    private static readonly Lazy<string[]> Merged = new(() => Lines("011-claude-key", "ui-test-ids.md"));

    private static readonly Lazy<string[]> Delta = new(() => Lines("011-claude-key", "ui-test-ids.delta.md"));

    [Fact]
    public void Merged_IsVersion1210()
    {
        (Base.Value[0], Merged.Value[0]).ShouldBe(("# Contract: UI test ids (v1.10.0)", "# Contract: UI test ids (v1.21.0)"));
    }

    [Fact]
    public void Merged_Contains1100LinesUnchangedAndInOrder()
    {
        // The 1.10.0 body (title aside) is the start of 1.21.0, apart from the inserted changelog line; the rest is appended.
        var body = Base.Value.Skip(1).ToList();

        Merged.Value.Skip(1).Where(line => !IsAdded(line)).Take(body.Count).ShouldBe(body);
    }

    private static bool IsAdded(string line) => Delta.Value.Contains(line) is false && Base.Value.Contains(line) is false;

    [Fact]
    public void Merged_ContainsEveryDeltaIdRow()
    {
        var rows = Delta.Value.Where(line => line.StartsWith("| `settings-agent-claude-key", StringComparison.Ordinal)).ToList();

        rows.Count.ShouldBe(14);
        rows.ShouldAllBe(row => Merged.Value.Contains(row));
    }

    [Fact]
    public void Merged_IdsAreNewAndUnique()
    {
        var added = Ids(Delta.Value).ToList();

        added.ShouldBeUnique();
        added.ShouldAllBe(id => !Ids(Base.Value).Contains(id));
        added.ShouldContain("settings-agent-claude-key-delete-confirm");
    }

    [Theory]
    [InlineData("agent-unavailable-switch-to-local")]
    [InlineData("agent-key-changed")]
    public void AgentPanelIds_AreDefinedOnceInDeltaAndMergedAndNotIn1100(string id)
    {
        var row = $"| Agent panel | `{id}` |";

        Delta.Value.Count(line => line.StartsWith(row, StringComparison.Ordinal)).ShouldBe(1);
        Merged.Value.Count(line => line.StartsWith(row, StringComparison.Ordinal)).ShouldBe(1);
        Base.Value.ShouldNotContain(line => line.Contains($"`{id}`", StringComparison.Ordinal));
    }

    [Fact]
    public void Merged_RecordsTheChangelogLine()
    {
        Merged.Value.ShouldContain(line => line.StartsWith("1.21.0 (additive, 011 follow-up claude-key", StringComparison.Ordinal));
    }

    // The id a table row defines: the first id of the row (later columns may mention other ids).
    private static IEnumerable<string> Ids(IEnumerable<string> lines) =>
        lines.Where(line => line.StartsWith('|')).Select(line => IdPattern().Match(line)).Where(match => match.Success).Select(match => match.Groups[1].Value);

    private static string[] Lines(string folder, string file) =>
        File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Contract", folder, file));

    [GeneratedRegex("`(settings-agent-claude-key[a-z-]*)`")]
    private static partial Regex IdPattern();
}
