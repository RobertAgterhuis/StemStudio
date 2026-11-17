namespace StemStudio.Api.Tests.Contract;

/// <summary>
/// Merge rule of the UI test ids (architect, 014 Phase 0 part 2, T005): the full <c>ui-test-ids.md</c> 1.13.0 is 013's 1.12.0
/// unchanged (title and the inserted changelog paragraph aside) plus the 014 <c>ui-test-ids.delta.md</c> ids unchanged, and no
/// 014 id repeats an earlier one (012's <c>practice-panel</c> stays the speed and key panel).
/// </summary>
public sealed class PracticeFlowUiTestIdsTests
{
    private static readonly Lazy<string[]> Base = new(() => Lines("013", "ui-test-ids.md"));

    private static readonly Lazy<string[]> Merged = new(() => Lines("014", "ui-test-ids.md"));

    private static readonly Lazy<string[]> Delta = new(() => Lines("014", "ui-test-ids.delta.md"));

    [Fact]
    public void Merged_IsVersion1130()
    {
        (Base.Value[0], Merged.Value[0]).ShouldBe(("# Contract: UI test ids (v1.12.0)", "# Contract: UI test ids (v1.13.0)"));
    }

    [Fact]
    public void Merged_Contains1120LinesUnchangedAndInOrder()
    {
        var body = Base.Value.Skip(1).ToList();

        Merged.Value.Skip(1).Where(line => Base.Value.Contains(line)).Take(body.Count).ShouldBe(body);
    }

    [Fact]
    public void Merged_HasTheDeltaIdsUnchanged()
    {
        var delta = Delta.Value.SkipWhile(line => line != "## Ids").Skip(1).ToList();
        var added = Merged.Value.SkipWhile(line => line != "## Additions in 1.13.0 (feature 014)").SkipWhile(line => line != "### Ids").Skip(1)
            .TakeWhile(line => line != "### Changed default keys (no id changes)").ToList();

        added.Where(line => line.Length > 0).ShouldBe(delta.Where(line => line.Length > 0));
        delta.Count(line => line.StartsWith("| ", StringComparison.Ordinal)).ShouldBeGreaterThan(40);
    }

    [Fact]
    public void DeltaIds_AreNewNames()
    {
        var earlier = string.Join('\n', Base.Value);
        var ids = Delta.Value.Where(line => line.StartsWith("| ", StringComparison.Ordinal)).SelectMany(line => line.Split('|')[2].Split(','))
            .Select(cell => cell.Trim().Trim('`')).Where(id => id.Length > 0 && !id.Contains(' ', StringComparison.Ordinal) && id != "shortcut-overlay" && id != "data-testid");

        ids.Where(id => earlier.Contains($"`{id}`", StringComparison.Ordinal)).ShouldBeEmpty();
    }

    [Fact]
    public void Merged_RecordsTheChangelogBeforeTheRules()
    {
        var changelog = Array.FindIndex(Merged.Value, line => line.StartsWith("1.13.0 (additive, feature 014", StringComparison.Ordinal));

        changelog.ShouldBeInRange(1, Array.IndexOf(Merged.Value, "## Rules") - 1);
    }

    private static string[] Lines(string folder, string file) =>
        File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Contract", folder, file));
}
