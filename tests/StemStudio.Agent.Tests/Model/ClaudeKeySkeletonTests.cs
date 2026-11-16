using StemStudio.Agent.Model;
using StemStudio.Agent.Ports;
using StemStudio.Contracts.Agent;
using StemStudio.TestDoubles.ClaudeKey;

namespace StemStudio.Agent.Tests.Model;

/// <summary>
/// Skeleton tests (architect, 011 follow-up claude-key Phase 0, T005): the Claude key model and port records. The
/// <see cref="ClaudeApiKey"/> behaviour itself is covered by <c>ClaudeApiKeyTests</c> (ai-agent T007).
/// </summary>
public sealed class ClaudeKeySkeletonTests
{
    [Fact]
    public void ClaudeApiKey_IsASealedClassNotARecord()
    {
        typeof(ClaudeApiKey).IsSealed.ShouldBeTrue();
        typeof(ClaudeApiKey).GetMethod("<Clone>$").ShouldBeNull();
        typeof(ClaudeApiKey).GetConstructors().ShouldBeEmpty();
    }

    [Fact]
    public void ClaudeApiKey_ToStringIsRedacted()
    {
        SentinelKey().ToString().ShouldBe(ClaudeApiKey.Redacted);
        ClaudeApiKey.Redacted.ShouldBe("[redacted]");
    }

    [Fact]
    public void FormatResult_ValidHoldsTheKey()
    {
        var key = SentinelKey();

        var result = ClaudeKeyFormatResult.Valid(key);

        (result.IsValid, result.Key, result.Error, result.ToString()).ShouldBe((true, key, (string?)null, "valid"));
    }

    [Fact]
    public void FormatResult_InvalidHoldsOnlyTheReason()
    {
        var result = ClaudeKeyFormatResult.Invalid(ClaudeKeyInvalidReasons.Prefix);

        (result.IsValid, result.Key, result.Error, result.ToString()).ShouldBe((false, (ClaudeApiKey?)null, "prefix", "invalid (prefix)"));
    }

    [Fact]
    public void FormatResult_RejectsMissingArguments()
    {
        Should.Throw<ArgumentNullException>(() => ClaudeKeyFormatResult.Valid(null!));
        Should.Throw<ArgumentException>(() => ClaudeKeyFormatResult.Invalid(" "));
    }

    [Fact]
    public void Resolution_IsConfiguredOnlyWithAKeyAndNeverPrintsIt()
    {
        var none = new ClaudeKeyResolution { State = ClaudeKeyStateDto.NotConfigured, Stored = StoredClaudeKeyStateDto.Absent, StoreAvailable = true };
        var stored = none with { Key = SentinelKey(), Source = ClaudeKeySourceDto.Stored, State = ClaudeKeyStateDto.Stored, LastFour = "STO1" };

        (none.Configured, stored.Configured).ShouldBe((false, true));
        stored.ToString().ShouldContain("Key = [redacted]");
    }

    [Fact]
    public void StoredKey_AbsentIsAUsableEmptyStore()
    {
        StoredClaudeKey.Absent.ShouldBe(new StoredClaudeKey { State = StoredClaudeKeyStateDto.Absent, StoreAvailable = true });
        StoredClaudeKey.Absent.Key.ShouldBeNull();
    }

    [Fact]
    public void SelectedKey_CarriesItsSource()
    {
        var selected = new SelectedClaudeKey(SentinelKey(), ClaudeKeySourceDto.Environment);

        selected.Source.ShouldBe(ClaudeKeySourceDto.Environment);
        selected.ToString().ShouldContain("[redacted]");
    }

    [Fact]
    public void StoreUnavailableReasons_AreTheDataModelCodes()
    {
        string[] codes =
        [
            KeyStoreUnavailableReasons.NotWritable, KeyStoreUnavailableReasons.InsideLibrary, KeyStoreUnavailableReasons.InsideBackups,
            KeyStoreUnavailableReasons.InsideApp, KeyStoreUnavailableReasons.InsideRepository,
        ];

        codes.ShouldBe(["not-writable", "inside-library", "inside-backups", "inside-app", "inside-repository"]);
    }

    private static ClaudeApiKey SentinelKey() => ClaudeApiKey.Parse(ClaudeKeySentinels.Stored).Key!;
}
