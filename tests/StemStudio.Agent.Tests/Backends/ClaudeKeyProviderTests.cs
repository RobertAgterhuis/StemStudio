using NSubstitute;
using StemStudio.Agent.Backends;
using StemStudio.Agent.Model;
using StemStudio.Agent.Ports;
using StemStudio.Contracts.Agent;
using StemStudio.TestDoubles.ClaudeKey;

namespace StemStudio.Agent.Tests.Backends;

/// <summary>
/// <see cref="ClaudeKeyProvider"/> (011 follow-up claude-key T009, CK-FR-001, CK-FR-008, CK-FR-011): the state table of
/// data-model 3 over every combination of environment key and stored row, the environment winning, whitespace as unset, the
/// store-unavailable passthrough, the readable reason and fix texts, the cache and the per-source selection for a test.
/// </summary>
public sealed class ClaudeKeyProviderTests
{
    private const string KeyVariable = "ANTHROPIC_API_KEY";
    private static readonly DateTimeOffset StoredAt = new(2026, 11, 16, 15, 40, 0, TimeSpan.Zero);

    private readonly IEnvironmentReader _environment = Substitute.For<IEnvironmentReader>();
    private readonly FakeStoredClaudeKeyReader _stored = new();

    private static ClaudeApiKey Key(string value) => ClaudeApiKey.Parse(value).Key!;

    private static StoredClaudeKey Readable() => new()
    {
        State = StoredClaudeKeyStateDto.Readable,
        Key = Key(ClaudeKeySentinels.Stored),
        LastFour = "STO1",
        StoredAt = StoredAt,
        StoreAvailable = true,
    };

    private static StoredClaudeKey Unreadable() => FakeStoredClaudeKeyReader.Unreadable("STO1", StoredAt).Current;

    private ClaudeKeyProvider Provider(string? environmentKey, StoredClaudeKey stored)
    {
        _environment.GetVariable(KeyVariable).Returns(environmentKey);
        _stored.Current = stored;
        return new ClaudeKeyProvider(_environment, _stored);
    }

    [Fact]
    public void EnvironmentKey_NothingStored_UsesTheEnvironment()
    {
        var resolution = Provider(ClaudeKeySentinels.Environment, StoredClaudeKey.Absent).Resolve();

        resolution.Key!.Reveal().ShouldBe(ClaudeKeySentinels.Environment);
        (resolution.State, resolution.Source, resolution.Configured, resolution.LastFour)
            .ShouldBe((ClaudeKeyStateDto.Environment, (ClaudeKeySourceDto?)ClaudeKeySourceDto.Environment, true, "ENV1"));
        (resolution.Stored, resolution.StoredLastFour, resolution.StoredAt, resolution.StoreAvailable)
            .ShouldBe((StoredClaudeKeyStateDto.Absent, (string?)null, (DateTimeOffset?)null, true));
        (resolution.Reason, resolution.Fix).ShouldBe((null, null));
    }

    [Fact]
    public void EnvironmentKey_WinsOverAReadableStoredKey()
    {
        var resolution = Provider(ClaudeKeySentinels.Environment, Readable()).Resolve();

        resolution.Key!.Reveal().ShouldBe(ClaudeKeySentinels.Environment);
        (resolution.State, resolution.Source, resolution.LastFour).ShouldBe((ClaudeKeyStateDto.Environment, (ClaudeKeySourceDto?)ClaudeKeySourceDto.Environment, "ENV1"));
        (resolution.Stored, resolution.StoredLastFour, resolution.StoredAt).ShouldBe((StoredClaudeKeyStateDto.Readable, "STO1", (DateTimeOffset?)StoredAt));
        (resolution.Reason, resolution.Fix).ShouldBe((null, null));
    }

    [Fact]
    public void EnvironmentKey_WithAnUnreadableStoredKey_IsUsedAndTheStoredKeyIsReportedWithTheFix()
    {
        var resolution = Provider(ClaudeKeySentinels.Environment, Unreadable()).Resolve();

        (resolution.State, resolution.Configured, resolution.LastFour).ShouldBe((ClaudeKeyStateDto.Environment, true, "ENV1"));
        (resolution.Stored, resolution.StoredLastFour).ShouldBe((StoredClaudeKeyStateDto.Unreadable, "STO1"));
        (resolution.Reason, resolution.Fix).ShouldBe((ClaudeKeyProvider.UnreadableReason, ClaudeKeyProvider.UnreadableFix));
    }

    [Fact]
    public void NoEnvironmentKey_ReadableStoredKey_UsesTheStoredKey()
    {
        var resolution = Provider(null, Readable()).Resolve();

        resolution.Key!.Reveal().ShouldBe(ClaudeKeySentinels.Stored);
        (resolution.State, resolution.Source, resolution.Configured, resolution.LastFour)
            .ShouldBe((ClaudeKeyStateDto.Stored, (ClaudeKeySourceDto?)ClaudeKeySourceDto.Stored, true, "STO1"));
        (resolution.Stored, resolution.StoredLastFour, resolution.StoredAt).ShouldBe((StoredClaudeKeyStateDto.Readable, "STO1", (DateTimeOffset?)StoredAt));
        (resolution.Reason, resolution.Fix).ShouldBe((null, null));
    }

    [Fact]
    public void NoEnvironmentKey_UnreadableStoredKey_IsStoredUnreadableWithoutAKey()
    {
        var resolution = Provider(null, Unreadable()).Resolve();

        resolution.Key.ShouldBeNull();
        (resolution.State, resolution.Source, resolution.Configured, resolution.LastFour)
            .ShouldBe((ClaudeKeyStateDto.StoredUnreadable, (ClaudeKeySourceDto?)null, false, (string?)null));
        (resolution.Stored, resolution.StoredLastFour, resolution.StoredAt).ShouldBe((StoredClaudeKeyStateDto.Unreadable, "STO1", (DateTimeOffset?)StoredAt));
        resolution.Reason.ShouldBe("The stored key cannot be read on this installation (the encryption keys are missing or different, e.g. after a restore on another computer).");
        resolution.Fix.ShouldBe("Enter the key again.");
    }

    [Fact]
    public void NoKeyAnywhere_IsNotConfigured()
    {
        var resolution = Provider(null, StoredClaudeKey.Absent).Resolve();

        resolution.ShouldBe(new ClaudeKeyResolution { State = ClaudeKeyStateDto.NotConfigured, Stored = StoredClaudeKeyStateDto.Absent, StoreAvailable = true });
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public void WhitespaceEnvironmentValue_CountsAsUnset(string value)
    {
        var resolution = Provider(value, Readable()).Resolve();

        (resolution.State, resolution.Source, resolution.LastFour).ShouldBe((ClaudeKeyStateDto.Stored, (ClaudeKeySourceDto?)ClaudeKeySourceDto.Stored, "STO1"));
        (resolution.Reason, resolution.Fix).ShouldBe((null, null));
    }

    [Fact]
    public void EnvironmentValue_IsTrimmed()
    {
        var resolution = Provider("  " + ClaudeKeySentinels.Environment + "\n", StoredClaudeKey.Absent).Resolve();

        resolution.Key!.Reveal().ShouldBe(ClaudeKeySentinels.Environment);
    }

    [Fact]
    public void MalformedEnvironmentValue_IsNotUsedAndSaysWhy()
    {
        var resolution = Provider(ClaudeKeySentinels.Prefix + "short", StoredClaudeKey.Absent).Resolve();

        (resolution.State, resolution.Configured, resolution.Source).ShouldBe((ClaudeKeyStateDto.NotConfigured, false, (ClaudeKeySourceDto?)null));
        resolution.Reason.ShouldBe(ClaudeKeyProvider.MalformedEnvironmentReason(ClaudeKeyInvalidReasons.Length));
        resolution.Reason!.ShouldContain("ANTHROPIC_API_KEY");
        resolution.Reason!.ShouldNotContain("short");
        resolution.Fix.ShouldBe(ClaudeKeyProvider.MalformedEnvironmentFix);
    }

    [Fact]
    public void MalformedEnvironmentValue_LeavesAReadableStoredKeyInUse()
    {
        var resolution = Provider(ClaudeKeySentinels.Admin, Readable()).Resolve();

        (resolution.State, resolution.Source, resolution.LastFour).ShouldBe((ClaudeKeyStateDto.Stored, (ClaudeKeySourceDto?)ClaudeKeySourceDto.Stored, "STO1"));
        resolution.Reason.ShouldBe(ClaudeKeyProvider.MalformedEnvironmentReason(ClaudeKeyInvalidReasons.AdminKey));
    }

    [Theory]
    [InlineData(KeyStoreUnavailableReasons.NotWritable, "missing or not writable")]
    [InlineData(KeyStoreUnavailableReasons.InsideLibrary, "inside the library folder")]
    [InlineData(KeyStoreUnavailableReasons.InsideBackups, "inside the backups folder")]
    [InlineData(KeyStoreUnavailableReasons.InsideApp, "inside the application folder")]
    [InlineData(KeyStoreUnavailableReasons.InsideRepository, "inside a git repository")]
    [InlineData("something-new", "cannot be used")]
    public void UnavailableStore_IsPassedThroughWithAReadableReason(string code, string words)
    {
        var resolution = Provider(null, FakeStoredClaudeKeyReader.StoreUnavailable(code).Current).Resolve();

        (resolution.State, resolution.StoreAvailable, resolution.StoreUnavailableReason).ShouldBe((ClaudeKeyStateDto.NotConfigured, false, code));
        resolution.Reason.ShouldBe($"The key store is not available: the key ring directory {words}. The ANTHROPIC_API_KEY environment variable still works.");
        resolution.Fix.ShouldBe(ClaudeKeyProvider.StoreUnavailableFix);
    }

    [Fact]
    public void UnavailableStore_WithAnEnvironmentKey_StillUsesTheEnvironment()
    {
        var stored = Unreadable() with { StoreAvailable = false, StoreUnavailableReason = KeyStoreUnavailableReasons.NotWritable };

        var resolution = Provider(ClaudeKeySentinels.Environment, stored).Resolve();

        (resolution.State, resolution.Configured, resolution.Stored, resolution.StoreAvailable)
            .ShouldBe((ClaudeKeyStateDto.Environment, true, StoredClaudeKeyStateDto.Unreadable, false));
        resolution.Reason.ShouldBe(ClaudeKeyProvider.StoreUnavailableReason(KeyStoreUnavailableReasons.NotWritable));
    }

    [Fact]
    public void UnavailableStoreWithoutAReasonCode_IsNotWritable()
    {
        var stored = StoredClaudeKey.Absent with { StoreAvailable = false };

        var resolution = Provider(null, stored).Resolve();

        resolution.StoreUnavailableReason.ShouldBeNull();
        resolution.Reason.ShouldBe(ClaudeKeyProvider.StoreUnavailableReason(null));
        resolution.Reason!.ShouldContain("cannot be used");
    }

    [Fact]
    public void Resolve_IsCachedUntilInvalidated()
    {
        var provider = Provider(null, Readable());

        var first = provider.Resolve();
        var second = provider.Resolve();
        _stored.Current = StoredClaudeKey.Absent;
        var stillCached = provider.Resolve();
        provider.Invalidate();
        var fresh = provider.Resolve();

        _stored.ReadCount.ShouldBe(2);
        second.ShouldBeSameAs(first);
        stillCached.State.ShouldBe(ClaudeKeyStateDto.Stored);
        fresh.State.ShouldBe(ClaudeKeyStateDto.NotConfigured);
    }

    [Fact]
    public void Resolve_FollowsAChangedEnvironmentValueWithoutInvalidate()
    {
        var provider = Provider(null, Readable());

        var before = provider.Resolve();
        _environment.GetVariable(KeyVariable).Returns(ClaudeKeySentinels.Environment);
        var after = provider.Resolve();
        _environment.GetVariable(KeyVariable).Returns((string?)null);
        var back = provider.Resolve();

        (before.State, after.State, back.State).ShouldBe((ClaudeKeyStateDto.Stored, ClaudeKeyStateDto.Environment, ClaudeKeyStateDto.Stored));
        _stored.ReadCount.ShouldBe(3);
    }

    [Fact]
    public void KeyFor_PicksTheRequestedSource()
    {
        var provider = Provider(ClaudeKeySentinels.Environment, Readable());

        var active = provider.KeyFor(ClaudeKeyTestSourceDto.Active)!;
        var environment = provider.KeyFor(ClaudeKeyTestSourceDto.Environment)!;
        var stored = provider.KeyFor(ClaudeKeyTestSourceDto.Stored)!;

        (active.Source, active.Key.Reveal()).ShouldBe((ClaudeKeySourceDto.Environment, ClaudeKeySentinels.Environment));
        (environment.Source, environment.Key.Reveal()).ShouldBe((ClaudeKeySourceDto.Environment, ClaudeKeySentinels.Environment));
        (stored.Source, stored.Key.Reveal()).ShouldBe((ClaudeKeySourceDto.Stored, ClaudeKeySentinels.Stored));
    }

    [Fact]
    public void KeyFor_ActiveFollowsThePrecedenceToTheStoredKey()
    {
        var active = Provider(null, Readable()).KeyFor(ClaudeKeyTestSourceDto.Active)!;

        (active.Source, active.Key.Reveal()).ShouldBe((ClaudeKeySourceDto.Stored, ClaudeKeySentinels.Stored));
    }

    [Fact]
    public void KeyFor_ReturnsNullForASourceWithoutAUsableKey()
    {
        var unreadable = Provider(null, Unreadable());

        unreadable.KeyFor(ClaudeKeyTestSourceDto.Active).ShouldBeNull();
        unreadable.KeyFor(ClaudeKeyTestSourceDto.Stored).ShouldBeNull();
        unreadable.KeyFor(ClaudeKeyTestSourceDto.Environment).ShouldBeNull();
        Provider(ClaudeKeySentinels.Environment, StoredClaudeKey.Absent).KeyFor(ClaudeKeyTestSourceDto.Stored).ShouldBeNull();
        Provider(ClaudeKeySentinels.Prefix + "short", Readable()).KeyFor(ClaudeKeyTestSourceDto.Environment).ShouldBeNull();
    }

    [Fact]
    public void KeyFor_RejectsAnUnknownSource() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Provider(null, Readable()).KeyFor((ClaudeKeyTestSourceDto)99));

    [Fact]
    public void Resolution_NeverPrintsAKey()
    {
        var resolution = Provider(ClaudeKeySentinels.Environment, Readable()).Resolve();

        var text = resolution.ToString();

        foreach (var sentinel in new[] { ClaudeKeySentinels.Environment, ClaudeKeySentinels.Stored })
        {
            foreach (var form in ClaudeKeySentinels.SearchForms(sentinel))
            {
                text.ShouldNotContain(form);
            }
        }
    }

    [Fact]
    public void Constructor_RejectsMissingPorts()
    {
        Should.Throw<ArgumentNullException>(() => new ClaudeKeyProvider(null!, _stored));
        Should.Throw<ArgumentNullException>(() => new ClaudeKeyProvider(_environment, null!));
    }
}
