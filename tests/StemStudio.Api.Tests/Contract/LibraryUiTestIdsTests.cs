using System.Text.RegularExpressions;

namespace StemStudio.Api.Tests.Contract;

/// <summary>
/// Merge rule of the UI test ids (architect, 022 Phase 0 T004; re-based onto 013 at the 022+023 round start): the full <c>ui-test-ids.md</c> 1.22.0 is
/// the 013 document (1.12.0) unchanged (title and the inserted changelog paragraph aside) plus the 022 <c>ui-test-ids.delta.md</c> from its
/// "Changed behaviour" section on; nothing is renamed or removed.
/// </summary>
public sealed partial class LibraryUiTestIdsTests
{
    // The two ids of 1.5.0 the 022 delta lists again (changed behaviour / reused rows), never as new ids.
    private static readonly string[] ReusedIds = ["asset-row-<assetId>", "library-search"];

    private static readonly Lazy<string[]> Base = new(() => Lines("013", "ui-test-ids.md"));

    private static readonly Lazy<string[]> Merged = new(() => Lines("022", "ui-test-ids.md"));

    private static readonly Lazy<string[]> Delta = new(() => Lines("022", "ui-test-ids.delta.md"));

    [Fact]
    public void Merged_IsVersion1220()
    {
        (Base.Value[0], Merged.Value[0]).ShouldBe(("# Contract: UI test ids (v1.12.0)", "# Contract: UI test ids (v1.22.0)"));
    }

    [Fact]
    public void Merged_Contains1120LinesUnchangedAndInOrder()
    {
        var body = Base.Value.Skip(1).ToList();

        Merged.Value.Skip(1).Where(line => Base.Value.Contains(line)).Take(body.Count).ShouldBe(body);
    }

    [Fact]
    public void Merged_EndsWithTheDeltaFromItsChangedBehaviourSection()
    {
        var from = Array.IndexOf(Delta.Value, "## Changed behaviour of existing ids (owner request; migration below)");
        var delta = Delta.Value.Skip(from).Select(line => line.StartsWith("## ", StringComparison.Ordinal) ? "#" + line : line).ToList();

        from.ShouldBePositive();
        Merged.Value.TakeLast(delta.Count).ShouldBe(delta);
        Merged.Value.ShouldContain("## Additions in 1.22.0 (feature 022)");
    }

    [Fact]
    public void Merged_IdsAreNewAndUnique()
    {
        var known = Base.Value.Where(line => line.StartsWith('|')).SelectMany(line => line.Split('`')).ToHashSet(StringComparer.Ordinal);
        var ids = IdRows().SelectMany(row => IdPattern().Matches(row.Split('|')[2]).Select(match => match.Groups[1].Value)).ToList();

        (IdRows().Count(), ids.Count, ids.Distinct().Count()).ShouldBe((39, 48, 48));
        ids.Where(known.Contains).ShouldBe(ReusedIds, ignoreOrder: true);
        ids.ShouldContain("library-folders");
        ids.ShouldContain("export-saved-folder");
        IdRows().ShouldAllBe(row => Merged.Value.Contains(row));
    }

    [Fact]
    public void Merged_RecordsTheChangelogLineBeforeTheRules()
    {
        var changelog = Array.FindIndex(Merged.Value, line => line.StartsWith("1.22.0 (minor, feature 022", StringComparison.Ordinal));

        changelog.ShouldBePositive();
        changelog.ShouldBeLessThan(Array.IndexOf(Merged.Value, "## Rules"));
    }

    private static IEnumerable<string> IdRows()
    {
        var start = Array.IndexOf(Delta.Value, "## Ids");
        var end = Array.FindIndex(Delta.Value, line => line.StartsWith("## Help topics", StringComparison.Ordinal));
        return Delta.Value[start..end].Where(line => line.StartsWith("| ", StringComparison.Ordinal) && !line.StartsWith("| Screen", StringComparison.Ordinal));
    }

    private static string[] Lines(string folder, string file) =>
        File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Contract", folder, file));

    [GeneratedRegex("`([a-z][A-Za-z0-9<>-]*)`")]
    private static partial Regex IdPattern();
}
