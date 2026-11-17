namespace StemStudio.Api.Tests.Contract;

/// <summary>
/// Merge rule of the UI test ids (architect, 013 Phase 0 T005): the full <c>ui-test-ids.md</c> 1.12.0 is 012's 1.11.0 unchanged
/// (title and the inserted changelog line aside) plus the 013 <c>ui-test-ids.delta.md</c> from "Template parts" on, unchanged
/// except the heading levels; nothing is renamed or removed.
/// </summary>
public sealed class LibraryCareUiTestIdsTests
{
    private const string Section = "## Additions in 1.12.0 (feature 013)";

    private static readonly Lazy<string[]> Base = new(() => Lines("012", "ui-test-ids.md"));

    private static readonly Lazy<string[]> Merged = new(() => Lines("013", "ui-test-ids.md"));

    private static readonly Lazy<string[]> Delta = new(() => Lines("013", "ui-test-ids.delta.md"));

    [Fact]
    public void Merged_IsVersion1120()
    {
        (Base.Value[0], Merged.Value[0]).ShouldBe(("# Contract: UI test ids (v1.11.0)", "# Contract: UI test ids (v1.12.0)"));
    }

    [Fact]
    public void Merged_Contains1110LinesUnchangedAndInOrder()
    {
        var body = Base.Value.Skip(1).ToList();

        Merged.Value.Skip(1).Where(line => !Base.Value.Contains(line)).ShouldNotBeEmpty();
        Merged.Value.Skip(1).Where(line => Base.Value.Contains(line)).Take(body.Count).ShouldBe(body);
    }

    [Fact]
    public void Merged_EndsWithTheDeltaBodyUnchanged()
    {
        var delta = Delta.Value.SkipWhile(line => line != "## Template parts").Select(Demote).ToList();
        var added = Merged.Value.SkipWhile(line => line != "### Template parts").ToList();

        Merged.Value.ShouldContain(Section);
        added.ShouldBe(delta);
        delta.Count(line => line.StartsWith("| ", StringComparison.Ordinal)).ShouldBeGreaterThan(70);
    }

    [Fact]
    public void Merged_RecordsTheChangelogLineBeforeTheRules()
    {
        var changelog = Array.FindIndex(Merged.Value, line => line.StartsWith("1.12.0 (additive, feature 013", StringComparison.Ordinal));
        var rules = Array.IndexOf(Merged.Value, "## Rules");

        changelog.ShouldBeInRange(1, rules - 1);
    }

    private static string Demote(string line) => line.StartsWith("## ", StringComparison.Ordinal) ? "#" + line : line;

    private static string[] Lines(string folder, string file) =>
        File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Contract", folder, file));
}
