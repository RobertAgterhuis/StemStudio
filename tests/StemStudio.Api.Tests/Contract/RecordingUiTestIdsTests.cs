namespace StemStudio.Api.Tests.Contract;

/// <summary>
/// Merge rule of the UI test ids (architect, 015 Phase 0 T002): the full <c>ui-test-ids.md</c> 1.14.0 is 014's 1.13.0 (re-based at the 014+015 round start; 013's 1.12.0 in Phase 0) unchanged (title
/// and the inserted changelog line aside) plus the 015 <c>ui-test-ids.delta.md</c> table unchanged; only the delta's level rule is
/// numbered (R10); nothing is renamed or removed.
/// </summary>
public sealed class RecordingUiTestIdsTests
{
    private const string Section = "## Additions in 1.14.0 (feature 015)";

    private static readonly Lazy<string[]> Base = new(() => Lines("014", "ui-test-ids.md"));

    private static readonly Lazy<string[]> Merged = new(() => Lines("015", "ui-test-ids.md"));

    private static readonly Lazy<string[]> Delta = new(() => Lines("015", "ui-test-ids.delta.md"));

    [Fact]
    public void Merged_IsVersion1140()
    {
        (Base.Value[0], Merged.Value[0]).ShouldBe(("# Contract: UI test ids (v1.13.0)", "# Contract: UI test ids (v1.14.0)"));
    }

    [Fact]
    public void Merged_Contains1130LinesUnchangedAndInOrder()
    {
        var body = Base.Value.Skip(1).ToList();

        Merged.Value.Skip(1).Where(line => Base.Value.Contains(line)).Take(body.Count).ShouldBe(body);
    }

    [Fact]
    public void Merged_EndsWithTheDeltaTableUnchanged()
    {
        var table = Delta.Value.SkipWhile(line => !line.StartsWith("| Screen |", StringComparison.Ordinal)).TakeWhile(line => !line.StartsWith("Level rule", StringComparison.Ordinal)).ToList();
        var added = Merged.Value.SkipWhile(line => line != Section).SkipWhile(line => !line.StartsWith("| Screen |", StringComparison.Ordinal))
            .TakeWhile(line => !line.StartsWith("**R10**", StringComparison.Ordinal)).ToList();

        added.ShouldBe(table);
        table.Count(line => line.StartsWith("| ", StringComparison.Ordinal)).ShouldBeGreaterThan(60);
        Merged.Value.ShouldContain(line => line.StartsWith("**R10** (level rule, 015", StringComparison.Ordinal));
    }

    [Fact]
    public void Merged_RecordsTheChangelogLineBeforeTheRules()
    {
        var changelog = Array.FindIndex(Merged.Value, line => line.StartsWith("1.14.0 (additive, feature 015", StringComparison.Ordinal));
        var rules = Array.IndexOf(Merged.Value, "## Rules");

        changelog.ShouldBeInRange(1, rules - 1);
    }

    [Fact]
    public void UnavailableReasons_MapEveryEngineReasonCode()
    {
        var mapping = Merged.Value.SkipWhile(line => line != Section).First(line => line.StartsWith("`insecureContext` -> `insecure`", StringComparison.Ordinal));

        new[] { "noMediaDevices", "noAudioWorklet", "rateMismatch" }.ShouldAllBe(code => mapping.Contains($"`{code}`", StringComparison.Ordinal));
    }

    private static string[] Lines(string folder, string file) =>
        File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Contract", folder, file));
}
