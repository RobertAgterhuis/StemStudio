using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using StemStudio.Agent.Model;
using StemStudio.Agent.Ports;
using StemStudio.Contracts.Agent;
using StemStudio.TestDoubles.ClaudeKey;

namespace StemStudio.Agent.Tests.Fakes;

/// <summary>Skeleton tests (architect, 011 follow-up claude-key Phase 0, T005): the shared test doubles and sentinels behave as documented.</summary>
public sealed partial class ClaudeKeyTestDoubleTests
{
    private static readonly DateTimeOffset StoredAt = new(2026, 11, 16, 15, 40, 0, TimeSpan.Zero);

    [Fact]
    public void Provider_ReturnsTheSetResolutionAndCountsCalls()
    {
        var provider = new FakeClaudeKeyProvider();
        var first = provider.Resolve();
        provider.Resolution = first with { Stored = StoredClaudeKeyStateDto.Unreadable, StoredLastFour = "STO1" };

        provider.Invalidate();

        (first, provider.Resolve().Stored, provider.ResolveCount, provider.InvalidateCount)
            .ShouldBe((FakeClaudeKeyProvider.NotConfigured, StoredClaudeKeyStateDto.Unreadable, 2, 1));
    }

    [Fact]
    public void Provider_KeyForReturnsOnlyConfiguredSources()
    {
        var selected = new SelectedClaudeKey(Key(), ClaudeKeySourceDto.Stored);
        var provider = new FakeClaudeKeyProvider().WithSelection(ClaudeKeyTestSourceDto.Stored, selected);

        (provider.KeyFor(ClaudeKeyTestSourceDto.Stored), provider.KeyFor(ClaudeKeyTestSourceDto.Environment)).ShouldBe((selected, (SelectedClaudeKey?)null));
        provider.Selected.ShouldBe([ClaudeKeyTestSourceDto.Stored, ClaudeKeyTestSourceDto.Environment]);
    }

    [Fact]
    public void Reader_DefaultsToAbsentAndCountsReads()
    {
        var reader = new FakeStoredClaudeKeyReader();

        reader.Read().ShouldBeSameAs(StoredClaudeKey.Absent);
        reader.ReadCount.ShouldBe(1);
    }

    [Fact]
    public void Reader_UnreadableKeepsLastFourAndTime()
    {
        var current = FakeStoredClaudeKeyReader.Unreadable("STO1", StoredAt).Read();

        (current.State, current.Key, current.LastFour, current.StoredAt, current.StoreAvailable)
            .ShouldBe((StoredClaudeKeyStateDto.Unreadable, (ClaudeApiKey?)null, "STO1", (DateTimeOffset?)StoredAt, true));
    }

    [Fact]
    public void Reader_StoreUnavailableCarriesTheReason()
    {
        var current = FakeStoredClaudeKeyReader.StoreUnavailable(KeyStoreUnavailableReasons.InsideRepository).Read();

        (current.State, current.StoreAvailable, current.StoreUnavailableReason).ShouldBe((StoredClaudeKeyStateDto.Absent, false, "inside-repository"));
    }

    [Theory]
    [MemberData(nameof(Sentinels))]
    public void Sentinels_AreWellFormedKeys(string sentinel)
    {
        sentinel.ShouldStartWith(ClaudeKeySentinels.Prefix);
        sentinel.Length.ShouldBeInRange(40, 256);
        KeyShape().IsMatch(sentinel).ShouldBeTrue();
    }

    [Fact]
    public void Sentinels_AreDistinctAndTheAdminOneIsAnAdminKey()
    {
        string[] lastFour = [.. Sentinels.Select(row => row.Data[^4..])];

        lastFour.ShouldBe(["ENV1", "STO1", "STO2"]);
        ClaudeKeySentinels.Admin.ShouldStartWith(ClaudeKeySentinels.Prefix + "admin");
        ClaudeKeySentinels.Prefix.Length.ShouldBe(7);
    }

    [Fact]
    public void Sentinels_SearchFormsCoverPlainBase64AndUtf16()
    {
        var forms = ClaudeKeySentinels.SearchForms(ClaudeKeySentinels.Stored);

        forms.Count.ShouldBe(3);
        forms[0].ShouldBe(ClaudeKeySentinels.Stored);
        forms.ShouldBeUnique();
    }

    public static TheoryData<string> Sentinels => new(ClaudeKeySentinels.Environment, ClaudeKeySentinels.Stored, ClaudeKeySentinels.Replacement);

    private static ClaudeApiKey Key() => (ClaudeApiKey)RuntimeHelpers.GetUninitializedObject(typeof(ClaudeApiKey));

    [GeneratedRegex("^[A-Za-z0-9_-]+$")]
    private static partial Regex KeyShape();
}
