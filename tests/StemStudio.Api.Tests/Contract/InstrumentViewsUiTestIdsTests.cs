namespace StemStudio.Api.Tests.Contract;

/// <summary>
/// Merge rule of the UI test ids (architect, 016 Phase 0 T004): the full <c>ui-test-ids.md</c> 1.15.0 is 015's 1.14.0 unchanged (title
/// and the inserted changelog line aside) plus the 016 <c>ui-test-ids.delta.md</c> sections unchanged; nothing is renamed or removed.
/// </summary>
public sealed class InstrumentViewsUiTestIdsTests
{
    private const string Section = "## Additions in 1.15.0 (feature 016)";

    private static readonly Lazy<string[]> Base = new(() => Lines("015", "ui-test-ids.md"));

    private static readonly Lazy<string[]> Merged = new(() => Lines("016", "ui-test-ids.md"));

    private static readonly Lazy<string[]> Delta = new(() => Lines("016", "ui-test-ids.delta.md"));

    [Fact]
    public void Merged_IsVersion1150()
    {
        (Base.Value[0], Merged.Value[0]).ShouldBe(("# Contract: UI test ids (v1.14.0)", "# Contract: UI test ids (v1.15.0)"));
    }

    [Fact]
    public void Merged_Contains1140LinesUnchangedAndInOrder()
    {
        var body = Base.Value.Skip(1).ToList();

        Merged.Value.Skip(1).Where(line => Base.Value.Contains(line)).Take(body.Count).ShouldBe(body);
    }

    [Fact]
    public void Merged_EndsWithTheDeltaTableAndAnchorsUnchanged()
    {
        var delta = Delta.Value.SkipWhile(line => line != "## Ids").Skip(1).TakeWhile(line => !line.StartsWith("## Phase 0", StringComparison.Ordinal))
            .Where(line => line.Length > 0 && !line.StartsWith("## ", StringComparison.Ordinal)).ToList();
        var added = Merged.Value.SkipWhile(line => line != Section).SkipWhile(line => line != "### Ids").Skip(1)
            .Where(line => line.Length > 0 && !line.StartsWith("### ", StringComparison.Ordinal)).ToList();

        added.ShouldBe(delta);
        delta.Count(line => line.StartsWith("| ", StringComparison.Ordinal)).ShouldBeGreaterThan(60);
    }

    [Fact]
    public void Merged_RecordsTheChangelogLineBeforeTheRules()
    {
        var changelog = Array.FindIndex(Merged.Value, line => line.StartsWith("1.15.0 (additive, feature 016", StringComparison.Ordinal));
        var rules = Array.IndexOf(Merged.Value, "## Rules");

        changelog.ShouldBeInRange(1, rules - 1);
    }

    [Fact]
    public void NewIds_DoNotCollideWithExistingTestIds()
    {
        var existing = Base.Value.SelectMany(TestIds).ToHashSet(StringComparer.Ordinal);
        var added = Merged.Value.SkipWhile(line => line != Section).Where(line => line.StartsWith("| ", StringComparison.Ordinal) && !line.StartsWith("| Screen", StringComparison.Ordinal)).SelectMany(TestIds).ToList();

        added.ShouldNotBeEmpty();
        added.Where(existing.Contains).ShouldBe(["karaoke-banner"]);  // the attribute row references the 006 host
    }

    private static IEnumerable<string> TestIds(string row)
    {
        var cells = row.Split('|');
        return cells.Length > 2
            ? cells[2].Split('`').Where((_, i) => i % 2 == 1).Where(id => id.Length > 0 && char.IsLower(id[0]) && !id.Contains(' ', StringComparison.Ordinal) && !id.Contains('=', StringComparison.Ordinal))
            : [];
    }

    private static string[] Lines(string folder, string file) =>
        File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Contract", folder, file));
}
