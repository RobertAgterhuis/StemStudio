namespace StemStudio.Api.Tests.Contract;

/// <summary>
/// Merge rule of the UI test ids (architect, 020 Phase 0 T003): the full <c>ui-test-ids.md</c> 1.19.0 is 016's 1.15.0 unchanged (title
/// and the inserted changelog line aside) plus the 020 <c>ui-test-ids.delta.md</c> "Ids" table and rules unchanged; the G2 ids are deferred.
/// </summary>
public sealed class InsightsUiTestIdsTests
{
    private const string Section = "## Additions in 1.19.0 (feature 020)";
    private static readonly Lazy<string[]> Base = new(() => Lines("016", "ui-test-ids.md"));
    private static readonly Lazy<string[]> Merged = new(() => Lines("020", "ui-test-ids.md"));
    private static readonly Lazy<string[]> Delta = new(() => Lines("020", "ui-test-ids.delta.md"));

    [Fact]
    public void Merged_IsVersion1190()
    {
        (Base.Value[0], Merged.Value[0]).ShouldBe(("# Contract: UI test ids (v1.15.0)", "# Contract: UI test ids (v1.19.0)"));
    }

    [Fact]
    public void Merged_Contains1150LinesUnchangedAndInOrder()
    {
        var body = Base.Value.Skip(1).ToList();

        Merged.Value.Skip(1).Where(line => Base.Value.Contains(line)).Take(body.Count).ShouldBe(body);
    }

    [Fact]
    public void Merged_EndsWithTheDeltaTableUnchanged()
    {
        var delta = Rows(Delta.Value.SkipWhile(line => line != "## Ids").Skip(1).TakeWhile(line => !line.StartsWith("## ", StringComparison.Ordinal)));
        var added = Rows(Merged.Value.SkipWhile(line => line != Section).SkipWhile(line => line != "### Ids").Skip(1)
            .TakeWhile(line => !line.StartsWith("### ", StringComparison.Ordinal)));

        added.ShouldBe(delta);
        delta.Count.ShouldBeGreaterThan(40);
    }

    [Fact]
    public void Merged_HasNoDeferredG2Id()
    {
        Merged.Value.ShouldNotContain(line => line.Contains("(G2)", StringComparison.Ordinal) || line.Contains("reseparate-dialog", StringComparison.Ordinal));
        Delta.Value.ShouldContain(line => line.Contains("reseparate-dialog", StringComparison.Ordinal));
    }

    [Fact]
    public void Merged_RecordsTheChangelogLineBeforeTheRules()
    {
        var changelog = Array.FindIndex(Merged.Value, line => line.StartsWith("1.19.0 (additive, feature 020", StringComparison.Ordinal));
        var rules = Array.IndexOf(Merged.Value, "## Rules");

        changelog.ShouldBeInRange(1, rules - 1);
    }

    [Fact]
    public void NewIds_DoNotCollideWithExistingTestIds()
    {
        var existing = Base.Value.SelectMany(TestIds).ToHashSet(StringComparer.Ordinal);
        var added = Merged.Value.SkipWhile(line => line != Section)
            .Where(line => line.StartsWith("| ", StringComparison.Ordinal) && !line.StartsWith("| Screen", StringComparison.Ordinal))
            .SelectMany(TestIds).ToList();

        added.ShouldNotBeEmpty();
        added.Where(existing.Contains).ShouldBe(["asset-preview-<assetId>", "asset-download-pdf-<assetId>"]);  // existing ids reused for handouts
    }

    private static List<string> Rows(IEnumerable<string> lines) => [.. lines.Where(line => line.StartsWith("| ", StringComparison.Ordinal))];

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
