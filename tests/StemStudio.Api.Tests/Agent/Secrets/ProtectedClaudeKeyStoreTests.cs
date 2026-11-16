using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using StemStudio.Agent.Model;
using StemStudio.Agent.Ports;
using StemStudio.Api.Agent.Secrets;
using StemStudio.Api.Storage;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts.Agent;
using StemStudio.Persistence;
using StemStudio.Persistence.Agent;
using StemStudio.TestDoubles.ClaudeKey;

namespace StemStudio.Api.Tests.Agent.Secrets;

/// <summary>011 follow-up claude-key T015: <see cref="ProtectedClaudeKeyStore"/> [sec] (CK-FR-002, CK-FR-011).</summary>
public sealed class ProtectedClaudeKeyStoreTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 11, 16, 12, 0, 0, TimeSpan.Zero);
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("stemstudio-cks-");
    private readonly List<ServiceProvider> _hosts = [];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private string Database => Path.Combine(_root.FullName, "data", "stemstudio.db");

    public void Dispose()
    {
        foreach (var host in _hosts)
        {
            host.Dispose();
        }

        SqliteConnection.ClearAllPools();
        _root.Delete(true);
    }

    [Fact]
    public void Read_WithoutARow_IsAbsentAndAvailable()
    {
        var host = Host("keys");

        var stored = host.Store.Read();

        stored.ShouldBe(StoredClaudeKey.Absent);
    }

    [Fact]
    public async Task StoreThenRead_RoundTripsTheKey()
    {
        var host = Host("keys");
        var key = Key(ClaudeKeySentinels.Stored);

        (await host.Store.StoreAsync(key, Ct)).ShouldBeFalse();
        var stored = host.Store.Read();

        stored.State.ShouldBe(StoredClaudeKeyStateDto.Readable);
        stored.Key.ShouldNotBeNull().Reveal().ShouldBe(ClaudeKeySentinels.Stored);
        (stored.LastFour, stored.StoredAt, stored.StoreAvailable, stored.StoreUnavailableReason).ShouldBe(("STO1", Start, true, null));
    }

    [Fact]
    public async Task Store_OverAnExistingKey_ReplacesItAndReportsReplaced()
    {
        var host = Host("keys");
        await host.Store.StoreAsync(Key(ClaudeKeySentinels.Stored), Ct);
        host.Clock.Advance(TimeSpan.FromMinutes(5));

        (await host.Store.StoreAsync(Key(ClaudeKeySentinels.Replacement), Ct)).ShouldBeTrue();

        var stored = host.Store.Read();
        (stored.Key!.Reveal(), stored.LastFour, stored.StoredAt).ShouldBe((ClaudeKeySentinels.Replacement, "STO2", Start.AddMinutes(5)));
    }

    [Fact]
    public async Task Store_WritesOnlyCiphertextLastFourAndTime()
    {
        var host = Host("keys");
        await host.Store.StoreAsync(Key(ClaudeKeySentinels.Stored), Ct);

        var row = host.Row().ShouldNotBeNull();
        (row.Name, row.LastFour, row.PurposeVersion, row.StoredAt).ShouldBe((AgentSecretEntity.ClaudeApiKeyName, "STO1", 1, Start.UtcDateTime));
        foreach (var form in ClaudeKeySentinels.SearchForms(ClaudeKeySentinels.Stored))
        {
            row.Ciphertext.ShouldNotContain(form);
        }
    }

    [Fact]
    public async Task DatabaseFiles_NeverContainThePlaintext()
    {
        var host = Host("keys");
        await host.Store.StoreAsync(Key(ClaudeKeySentinels.Stored), Ct);
        host.Store.Read();
        host.Dispose();
        SqliteConnection.ClearAllPools();

        foreach (var file in Directory.EnumerateFiles(Path.GetDirectoryName(Database)!, "stemstudio.db*"))
        {
            var bytes = await File.ReadAllBytesAsync(file, Ct);
            foreach (var form in ClaudeKeySentinels.SearchForms(ClaudeKeySentinels.Stored))
            {
                Contains(bytes, Encoding.UTF8.GetBytes(form)).ShouldBeFalse($"{form} in {file}");
            }

            Contains(bytes, Encoding.Unicode.GetBytes(ClaudeKeySentinels.Stored)).ShouldBeFalse(file);
        }
    }

    [Fact]
    public async Task CiphertextFromAnotherKeyRing_IsUnreadableButKeepsLastFourAndTime()
    {
        await Host("keys").Store.StoreAsync(Key(ClaudeKeySentinels.Stored), Ct);

        var other = Host("other-keys");
        var stored = other.Store.Read();

        (stored.State, stored.Key, stored.LastFour, stored.StoredAt, stored.StoreAvailable)
            .ShouldBe((StoredClaudeKeyStateDto.Unreadable, (ClaudeApiKey?)null, "STO1", (DateTimeOffset?)Start, true));
    }

    [Theory]
    [InlineData("not base64 !")]
    [InlineData("CfDJ8AAAAAAAAAAAAAAAAAAAAAA")]
    public async Task CorruptedCiphertext_IsUnreadable(string garbage)
    {
        var host = Host("keys");
        await host.Store.StoreAsync(Key(ClaudeKeySentinels.Stored), Ct);
        await host.ExecuteAsync($"UPDATE AgentSecrets SET Ciphertext = '{garbage}'");

        host.Store.Read().State.ShouldBe(StoredClaudeKeyStateDto.Unreadable);
    }

    [Fact]
    public async Task TamperedCiphertext_IsUnreadable()
    {
        var host = Host("keys");
        await host.Store.StoreAsync(Key(ClaudeKeySentinels.Stored), Ct);
        var ciphertext = host.Row()!.Ciphertext;
        var tampered = ciphertext[..^2] + (ciphertext[^2] == 'A' ? 'B' : 'A') + ciphertext[^1];
        await host.ExecuteAsync($"UPDATE AgentSecrets SET Ciphertext = '{tampered}'");

        host.Store.Read().State.ShouldBe(StoredClaudeKeyStateDto.Unreadable);
    }

    [Fact]
    public async Task DecryptedValueThatIsNoKey_IsUnreadable()
    {
        var host = Host("keys");
        await host.Store.StoreAsync(Key(ClaudeKeySentinels.Stored), Ct);
        var protector = host.Services.GetRequiredService<ClaudeKeyProtector>();
        await host.ExecuteAsync($"UPDATE AgentSecrets SET Ciphertext = '{protector.Protect("not a key")}'");

        host.Store.Read().State.ShouldBe(StoredClaudeKeyStateDto.Unreadable);
    }

    [Fact]
    public async Task RevokedKey_IsUnreadable()
    {
        var host = Host("keys");
        await host.Store.StoreAsync(Key(ClaudeKeySentinels.Stored), Ct);

        host.Services.GetRequiredService<IKeyManager>().RevokeAllKeys(DateTimeOffset.UtcNow.AddMinutes(1), "test");

        // Data Protection refreshes a changed key ring in the background (the first reads may still see the old ring).
        await Eventually.WaitUntilAsync(() => Task.FromResult(host.Store.Read().State == StoredClaudeKeyStateDto.Unreadable));
    }

    [Fact]
    public async Task PayloadOfANonDefaultKey_IsReprotectedAndSaved()
    {
        var host = Host("keys");
        await host.Store.StoreAsync(Key(ClaudeKeySentinels.Stored), Ct);
        var before = host.Row()!.Ciphertext;
        host.Services.GetRequiredService<IKeyManager>().CreateNewKey(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(90));
        host.Clock.Advance(TimeSpan.FromDays(1));
        StoredClaudeKey? stored = null;

        // Data Protection refreshes a changed key ring in the background (the first reads may still see the old key as default).
        await Eventually.WaitUntilAsync(() => Task.FromResult((stored = host.Store.Read()).StoredAt != Start));

        stored!.Key!.Reveal().ShouldBe(ClaudeKeySentinels.Stored);
        stored.StoredAt.ShouldBe(Start.AddDays(1));
        var row = host.Row()!;
        row.Ciphertext.ShouldNotBe(before);
        (row.LastFour, row.StoredAt).ShouldBe(("STO1", Start.AddDays(1).UtcDateTime));
        host.Clock.Advance(TimeSpan.FromDays(1));
        host.Store.Read().StoredAt.ShouldBe(Start.AddDays(1));
    }

    [Fact]
    public async Task Unreadable_IsLoggedOncePerProcessWithTheExceptionTypeOnly()
    {
        await Host("keys").Store.StoreAsync(Key(ClaudeKeySentinels.Stored), Ct);
        var other = Host("other-keys");
        var ciphertext = other.Row()!.Ciphertext;

        other.Store.Read();
        other.Store.Read();

        var entries = other.Logs.For(nameof(ProtectedClaudeKeyStore)).ToList();
        entries.Count.ShouldBe(1);
        entries[0].Level.ShouldBe(LogLevel.Warning);
        entries[0].Message.ShouldContain("CryptographicException");
        entries[0].Exception.ShouldBeNull();
        entries[0].Message.ShouldNotContain(ciphertext);
        foreach (var entry in other.Logs.Entries)
        {
            entry.Message.ShouldNotContain(ClaudeKeySentinels.Stored);
            entry.Message.ShouldNotContain(ciphertext);
        }
    }

    [Fact]
    public async Task StoreAndDelete_AreLoggedWithoutTheValueOrLastFour()
    {
        var host = Host("keys");
        await host.Store.StoreAsync(Key(ClaudeKeySentinels.Stored), Ct);
        await host.Store.StoreAsync(Key(ClaudeKeySentinels.Replacement), Ct);
        await host.Store.DeleteAsync(Ct);

        var messages = host.Logs.For(nameof(ProtectedClaudeKeyStore)).Select(entry => entry.Message).ToList();
        messages.Count.ShouldBe(3);
        messages[0].ShouldContain("replaced: False");
        messages[1].ShouldContain("replaced: True");
        messages[2].ShouldContain("deleted");
        messages.ShouldAllBe(message => !message.Contains("STO", StringComparison.Ordinal) && !message.Contains(ClaudeKeySentinels.Prefix, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Delete_RemovesTheKeyAndIsIdempotent()
    {
        var host = Host("keys");
        await host.Store.StoreAsync(Key(ClaudeKeySentinels.Stored), Ct);

        await host.Store.DeleteAsync(Ct);
        await host.Store.DeleteAsync(Ct);

        host.Store.Read().ShouldBe(StoredClaudeKey.Absent);
        host.Logs.For(nameof(ProtectedClaudeKeyStore)).Count(entry => entry.Message.Contains("deleted", StringComparison.Ordinal)).ShouldBe(1);
    }

    [Fact]
    public async Task UnavailableKeyRing_ReportsTheReasonRefusesStoringAndReadsAnOldRowAsUnreadable()
    {
        await Host("keys").Store.StoreAsync(Key(ClaudeKeySentinels.Stored), Ct);
        var unavailable = Host(Path.Combine("data", "keys"));

        var stored = unavailable.Store.Read();

        (stored.State, stored.StoreAvailable, stored.StoreUnavailableReason)
            .ShouldBe((StoredClaudeKeyStateDto.Unreadable, false, KeyStoreUnavailableReasons.InsideLibrary));
        await Should.ThrowAsync<InvalidOperationException>(() => unavailable.Store.StoreAsync(Key(ClaudeKeySentinels.Replacement), Ct));
        unavailable.Row()!.LastFour.ShouldBe("STO1");
        await unavailable.Store.DeleteAsync(Ct);
        unavailable.Store.Read().ShouldBe(StoredClaudeKey.Absent with { StoreAvailable = false, StoreUnavailableReason = KeyStoreUnavailableReasons.InsideLibrary });
    }

    [Fact]
    public void Store_IsRegisteredAsBothPortsOfOneSingleton()
    {
        var host = Host("keys");

        host.Services.GetRequiredService<IStoredClaudeKeyReader>().ShouldBeSameAs(host.Store);
        host.Services.GetRequiredService<IStoredClaudeKeyWriter>().ShouldBeSameAs(host.Store);
    }

    [Fact]
    public async Task Arguments_AreValidated()
    {
        var host = Host("keys");

        await Should.ThrowAsync<ArgumentNullException>(() => host.Store.StoreAsync(null!, Ct));
        Should.Throw<ArgumentNullException>(() => host.Services.GetRequiredService<ClaudeKeyProtector>().Protect(null!));
    }

    [Fact]
    public void Protector_WithoutPersistedSupport_StillDecrypts()
    {
        var inner = Substitute.For<IDataProtector>();
        inner.Protect(Arg.Any<byte[]>()).Returns(call => call.Arg<byte[]>());
        inner.Unprotect(Arg.Any<byte[]>()).Returns(call => call.Arg<byte[]>());
        var provider = Substitute.For<IDataProtectionProvider>();
        provider.CreateProtector(DataProtectionSetup.ClaudeKeyPurpose).Returns(inner);
        var protector = new ClaudeKeyProtector(provider, KeyRingStatus.Ready(_root.FullName, null));

        protector.Unprotect(protector.Protect("value")).ShouldBe(new UnprotectResult("value", false, null));
        Should.Throw<ArgumentNullException>(() => new ClaudeKeyProtector(null!, KeyRingStatus.Ready(_root.FullName, null)));
        Should.Throw<ArgumentNullException>(() => new ClaudeKeyProtector(provider, null!));
    }

    [Fact]
    public async Task DecryptedValueThatIsNoKey_IsLoggedAsAFormatFailure()
    {
        var host = Host("keys");
        await host.Store.StoreAsync(Key(ClaudeKeySentinels.Stored), Ct);
        await host.ExecuteAsync($"UPDATE AgentSecrets SET Ciphertext = '{host.Services.GetRequiredService<ClaudeKeyProtector>().Protect("not a key")}'");

        host.Store.Read();

        host.Logs.For(nameof(ProtectedClaudeKeyStore)).ShouldContain(entry => entry.Message.Contains("InvalidKeyFormat", StringComparison.Ordinal));
    }

    private static ClaudeApiKey Key(string value) => ClaudeApiKey.Parse(value).Key.ShouldNotBeNull();

    private static bool Contains(byte[] haystack, byte[] needle) => haystack.AsSpan().IndexOf(needle) >= 0;

    private StoreHost Host(string keyDir)
    {
        var host = new StoreHost(_root.FullName, Database, Path.Combine(_root.FullName, keyDir));
        _hosts.Add(host.Services);
        return host;
    }

    private sealed class StoreHost
    {
        public StoreHost(string root, string database, string keyDir)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(database)!);
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection([new KeyValuePair<string, string?>(DataProtectionSetup.KeyRingDirKey, keyDir)])
                .Build();
            var services = new ServiceCollection()
                .AddLogging(logging => logging.AddProvider(Logs))
                .AddSingleton<TimeProvider>(Clock)
                .AddStemStudioPersistence($"Data Source={database};Pooling=False")
                .AddClaudeKeyProtection(configuration, Path.Combine(root, "app"), new DataDirectory(Path.GetDirectoryName(database)!))
                .AddClaudeKeyStore(configuration);
            Services = services.BuildServiceProvider();
            using var scope = Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<StemStudioDbContext>().Database.Migrate();
        }

        public ServiceProvider Services { get; }

        public ListLoggerProvider Logs { get; } = new();

        public FakeTimeProvider Clock { get; } = new(Start);

        public ProtectedClaudeKeyStore Store => Services.GetRequiredService<ProtectedClaudeKeyStore>();

        public AgentSecretEntity? Row()
        {
            using var scope = Services.CreateScope();
            return scope.ServiceProvider.GetRequiredService<IAgentSecretStore>().Find(AgentSecretEntity.ClaudeApiKeyName);
        }

        public async Task ExecuteAsync(string sql)
        {
            await using var scope = Services.CreateAsyncScope();
#pragma warning disable EF1002 // test-only SQL with fixed values
            await scope.ServiceProvider.GetRequiredService<StemStudioDbContext>().Database.ExecuteSqlRawAsync(sql, Ct);
#pragma warning restore EF1002
        }

        public void Dispose() => Services.Dispose();
    }
}
