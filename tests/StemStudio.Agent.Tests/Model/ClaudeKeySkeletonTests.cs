using System.Runtime.CompilerServices;
using StemStudio.Agent.Model;
using StemStudio.Agent.Ports;
using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.Tests.Model;

/// <summary>
/// Skeleton tests (architect, 011 follow-up claude-key Phase 0, T005): the Claude key model and port records. The
/// <see cref="ClaudeApiKey"/> stub tests are replaced by the ai-agent's <c>ClaudeApiKeyTests</c> (T007).
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
    public void ClaudeApiKey_StubThrowsUntilTheAiAgentBuildsIt()
    {
        var key = Uninitialized();

        Should.Throw<NotImplementedException>(() => ClaudeApiKey.Parse("x"));
        Should.Throw<NotImplementedException>(key.Reveal);
        Should.Throw<NotImplementedException>(() => key.LastFour);
        Should.Throw<NotImplementedException>(() => key.Fingerprint);
    }

    [Fact]
    public void ClaudeApiKey_ToStringIsRedacted()
    {
        Uninitialized().ToString().ShouldBe(ClaudeApiKey.Redacted);
        ClaudeApiKey.Redacted.ShouldBe("[redacted]");
    }

    [Fact]
    public void FormatResult_ValidHoldsTheKey()
    {
        var key = Uninitialized();

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
        var stored = none with { Key = Uninitialized(), Source = ClaudeKeySourceDto.Stored, State = ClaudeKeyStateDto.Stored, LastFour = "STO1" };

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
        var selected = new SelectedClaudeKey(Uninitialized(), ClaudeKeySourceDto.Environment);

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

    // The stub has no public constructor and Parse throws; an uninitialized instance reaches the members until T008.
    private static ClaudeApiKey Uninitialized() => (ClaudeApiKey)RuntimeHelpers.GetUninitializedObject(typeof(ClaudeApiKey));
}
