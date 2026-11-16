using System.Net;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StemStudio.Agent.Model;
using StemStudio.Api.Agent.Secrets;
using StemStudio.Api.Storage;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;

namespace StemStudio.Api.Tests.Agent.Secrets;

/// <summary>011 follow-up claude-key T013: Data Protection set-up and the key ring directory guard [sec] (CK-FR-003..005).</summary>
public sealed class DataProtectionSetupTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("stemstudio-dp-");

    private string DataDir => Path.Combine(_root.FullName, "data");

    private string ContentRoot => Path.Combine(_root.FullName, "app");

    private string KeyDir => Path.Combine(_root.FullName, "keys");

    public void Dispose() => _root.Delete(true);

    [Fact]
    public void Constants_AreTheFixedValuesOfTheDesign()
    {
        DataProtectionSetup.ApplicationName.ShouldBe("StemStudio.Api");
        DataProtectionSetup.ClaudeKeyPurpose.ShouldBe("StemStudio.Agent.ClaudeApiKey.v1");
        DataProtectionSetup.KeyRingDirKey.ShouldBe("StemStudio:DataProtection:KeyRingDir");
        DataProtectionSetup.BackupDirKey.ShouldBe("StemStudio:BackupDir");
    }

    [Fact]
    public void PayloadFromTheSameDirectoryAndName_Decrypts()
    {
        var ciphertext = Protector(Build(KeyDir)).Protect("payload");

        Protector(Build(KeyDir)).Unprotect(ciphertext).ShouldBe("payload");
        Directory.EnumerateFiles(KeyDir, "key-*.xml").ShouldNotBeEmpty();
    }

    [Fact]
    public void PayloadUnderAnotherApplicationName_DoesNotDecrypt()
    {
        var ciphertext = Protector(Build(KeyDir)).Protect("payload");
        var other = DataProtectionProvider.Create(new DirectoryInfo(KeyDir), builder => builder.SetApplicationName("StemStudio.Web"));

        Should.Throw<CryptographicException>(() => other.CreateProtector(DataProtectionSetup.ClaudeKeyPurpose).Unprotect(ciphertext));
    }

    [Fact]
    public void PayloadUnderAnotherPurpose_DoesNotDecrypt()
    {
        var provider = Build(KeyDir).GetRequiredService<IDataProtectionProvider>();
        var ciphertext = provider.CreateProtector(DataProtectionSetup.ClaudeKeyPurpose).Protect("payload");

        Should.Throw<CryptographicException>(() => provider.CreateProtector("StemStudio.Agent.ClaudeApiKey.v2").Unprotect(ciphertext));
    }

    [Fact]
    public void UsableDirectory_IsAvailableAndPersistsTheRingThere()
    {
        var services = Build(KeyDir);

        var status = services.GetRequiredService<KeyRingStatus>();
        (status.Available, status.UnavailableReason, status.Path).ShouldBe((true, null, KeyDir));
        var options = services.GetRequiredService<IOptions<KeyManagementOptions>>().Value;
        options.XmlRepository.ShouldNotBeNull();
        services.GetRequiredService<IOptions<DataProtectionOptions>>().Value.ApplicationDiscriminator.ShouldBe("StemStudio.Api");
    }

    [Fact]
    public void Windows_ProtectsTheRingWithDpapi()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "DPAPI exists only on Windows");

        var options = Build(KeyDir).GetRequiredService<IOptions<KeyManagementOptions>>().Value;

        options.XmlEncryptor.ShouldBeOfType<DpapiXmlEncryptor>();
        Protector(Build(KeyDir)).Protect("payload");
        File.ReadAllText(Directory.EnumerateFiles(KeyDir, "key-*.xml").Single()).ShouldContain("encryptedSecret");
    }

    [Fact]
    public void NonWindows_HasNoAtRestEncryptor()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "the Linux/macOS path has no at-rest encryption (research R-02)");

        Build(KeyDir).GetRequiredService<IOptions<KeyManagementOptions>>().Value.XmlEncryptor.ShouldBeNull();
    }

    [Theory]
    [InlineData("data", KeyStoreUnavailableReasons.InsideLibrary)]
    [InlineData("data/keys", KeyStoreUnavailableReasons.InsideLibrary)]
    [InlineData("backups/keys", KeyStoreUnavailableReasons.InsideBackups)]
    [InlineData("app/keys", KeyStoreUnavailableReasons.InsideApp)]
    [InlineData("repo/sub/keys", KeyStoreUnavailableReasons.InsideRepository)]
    public void MisplacedDirectory_IsRefusedAndNothingIsWrittenThere(string relative, string reason)
    {
        Directory.CreateDirectory(Path.Combine(_root.FullName, "repo", ".git"));
        var keyDir = Path.Combine(_root.FullName, relative);

        var services = Build(keyDir, backupDir: Path.Combine(_root.FullName, "backups"));

        var status = services.GetRequiredService<KeyRingStatus>();
        (status.Available, status.UnavailableReason).ShouldBe((false, reason));
        Protector(services).Unprotect(Protector(services).Protect("payload")).ShouldBe("payload");
        (Directory.Exists(keyDir) && Directory.EnumerateFiles(keyDir).Any()).ShouldBeFalse();
    }

    [Fact]
    public void GitFileOfAWorktree_CountsAsARepository()
    {
        Directory.CreateDirectory(Path.Combine(_root.FullName, "worktree"));
        File.WriteAllText(Path.Combine(_root.FullName, "worktree", ".git"), "gitdir: elsewhere");

        var status = KeyRingDirectory.Prepare(Locations(Path.Combine(_root.FullName, "worktree", "keys")), FakeModes.Unsupported);

        status.UnavailableReason.ShouldBe(KeyStoreUnavailableReasons.InsideRepository);
    }

    [Fact]
    public void SiblingWithACommonPrefix_IsNotInside()
    {
        var status = KeyRingDirectory.Prepare(Locations(DataDir + "-keys"), FakeModes.Unsupported);

        (status.Available, status.UnavailableReason).ShouldBe((true, null));
    }

    [Fact]
    public void DirectoryThatCannotBeCreated_IsNotWritable()
    {
        var blocker = Path.Combine(_root.FullName, "blocker");
        File.WriteAllText(blocker, "a file, not a directory");

        var status = KeyRingDirectory.Prepare(Locations(Path.Combine(blocker, "keys")), FakeModes.Unsupported);

        (status.Available, status.UnavailableReason).ShouldBe((false, KeyStoreUnavailableReasons.NotWritable));
    }

    [Fact]
    public void MissingDirectory_IsCreated()
    {
        var status = KeyRingDirectory.Prepare(Locations(KeyDir), FakeModes.Unsupported);

        status.Available.ShouldBeTrue();
        Directory.Exists(KeyDir).ShouldBeTrue();
        Directory.EnumerateFileSystemEntries(KeyDir).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute)]
    [InlineData(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.OtherRead)]
    public void WideMode_IsTightenedTo0700(UnixFileMode mode)
    {
        var modes = new FakeModes { Mode = mode };

        var status = KeyRingDirectory.Prepare(Locations(KeyDir), modes);

        modes.Mode.ShouldBe(KeyRingDirectory.OwnerOnly);
        (status.Available, status.WideMode).ShouldBe((true, null));
    }

    [Fact]
    public void OwnerOnlyMode_IsLeftAlone()
    {
        var modes = new FakeModes { Mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute };

        KeyRingDirectory.Prepare(Locations(KeyDir), modes).WideMode.ShouldBeNull();

        modes.SetCalls.ShouldBe(0);
    }

    [Fact]
    public void WideModeThatCannotBeChanged_IsReportedButStaysAvailable()
    {
        var wide = KeyRingDirectory.OwnerOnly | UnixFileMode.GroupRead;
        var modes = new FakeModes { Mode = wide, RefuseSet = true };

        var status = KeyRingDirectory.Prepare(Locations(KeyDir), modes);

        (status.Available, status.WideMode).ShouldBe((true, wide));
    }

    [Fact]
    public void Linux_RealModesAreTightened()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux() || OperatingSystem.IsMacOS(), "Unix file modes");
        Directory.CreateDirectory(KeyDir);
        File.SetUnixFileMode(KeyDir, KeyRingDirectory.OwnerOnly | UnixFileMode.GroupRead | UnixFileMode.OtherRead);

        KeyRingDirectory.Prepare(Locations(KeyDir), UnixFileModes.ForCurrentPlatform()).Available.ShouldBeTrue();

        File.GetUnixFileMode(KeyDir).ShouldBe(KeyRingDirectory.OwnerOnly);
    }

    [Fact]
    public void RealModes_AreUnsupportedOnlyOnWindows() =>
        UnixFileModes.ForCurrentPlatform().Supported.ShouldBe(!OperatingSystem.IsWindows());

    [Fact]
    public void StartupWarnings_AreLoggedOnceWithReasonOrPathAndMode()
    {
        var logs = new ListLoggerProvider();
        using var factory = LoggerFactory.Create(builder => builder.AddProvider(logs));
        var logger = factory.CreateLogger("KeyRing");
        var unavailable = KeyRingStatus.Unavailable(KeyDir, KeyStoreUnavailableReasons.InsideLibrary);
        var wide = KeyRingStatus.Ready(KeyDir, KeyRingDirectory.OwnerOnly | UnixFileMode.OtherRead);

        unavailable.LogStartupWarnings(logger);
        unavailable.LogStartupWarnings(logger);
        wide.LogStartupWarnings(logger);
        KeyRingStatus.Ready(KeyDir, null).LogStartupWarnings(logger);

        var entries = logs.Entries;
        entries.Count.ShouldBe(2);
        entries[0].Level.ShouldBe(LogLevel.Warning);
        entries[0].Message.ShouldContain(KeyStoreUnavailableReasons.InsideLibrary);
        entries[1].Message.ShouldContain(KeyDir);
        entries[1].Message.ShouldContain("UserRead");
    }

    [Theory]
    [InlineData(true, @"C:\Users\owner\AppData\Local", null, null)]
    [InlineData(false, null, "/home/owner/.xdg", null)]
    [InlineData(false, null, null, "/home/owner")]
    public void DefaultDirectory_IsPerUser(bool windows, string? localAppData, string? xdgDataHome, string? home)
    {
        var variables = new Dictionary<string, string?> { ["LOCALAPPDATA"] = localAppData, ["XDG_DATA_HOME"] = xdgDataHome, ["HOME"] = home };

        var directory = KeyRingDefaults.Directory(windows, name => variables.GetValueOrDefault(name));

        directory.ShouldBe((windows, xdgDataHome) switch
        {
            (true, _) => Path.Combine(localAppData!, "StemStudio", "keys"),
            (false, not null) => Path.Combine(xdgDataHome, "stemstudio", "keys"),
            _ => Path.Combine(home!, ".local", "share", "stemstudio", "keys"),
        });
    }

    [Fact]
    public void DefaultDirectory_WithoutVariables_UsesTheUserProfileFolders()
    {
        KeyRingDefaults.Directory(true, _ => null)
            .ShouldBe(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StemStudio", "keys"));
        KeyRingDefaults.Directory(false, _ => null)
            .ShouldBe(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "stemstudio", "keys"));
        KeyRingDefaults.ForCurrentProcess().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void UnconfiguredDirectory_UsesTheDefault()
    {
        var configuration = new ConfigurationBuilder().Build();

        DataProtectionSetup.ConfiguredKeyRingDirectory(configuration, ContentRoot).ShouldBe(KeyRingDefaults.ForCurrentProcess());
    }

    [Fact]
    public void RelativeConfiguredDirectory_ResolvesAgainstTheContentRoot()
    {
        var configuration = Configuration(("StemStudio:DataProtection:KeyRingDir", "../keys"));

        DataProtectionSetup.ConfiguredKeyRingDirectory(configuration, ContentRoot).ShouldBe(KeyDir);
    }

    [Fact]
    public async Task ApiHost_WithAMisplacedDirectory_StillStartsAndWarns()
    {
        var dataDir = Directory.CreateDirectory(DataDir).FullName;
        var keyDir = Path.Combine(dataDir, "keys");
        using var factory = new ApiFactory(dataDir) { Settings = new Dictionary<string, string?> { [DataProtectionSetup.KeyRingDirKey] = keyDir } };
        using var client = factory.CreateClient();

        using var health = await client.GetAsync(new Uri(ApiRoutes.Health, UriKind.Relative), TestContext.Current.CancellationToken);

        health.StatusCode.ShouldBe(HttpStatusCode.OK);
        var status = factory.Services.GetRequiredService<KeyRingStatus>();
        (status.Available, status.UnavailableReason).ShouldBe((false, KeyStoreUnavailableReasons.InsideLibrary));
        factory.Logs.Entries.Count(entry => entry.Message.Contains(KeyStoreUnavailableReasons.InsideLibrary, StringComparison.Ordinal)).ShouldBe(1);
        Directory.Exists(keyDir).ShouldBeFalse();
    }

    [Fact]
    public async Task ApiHost_ByDefaultInTests_UsesAKeyRingOutsideTheDataDirectory()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        using var health = await client.GetAsync(new Uri(ApiRoutes.Health, UriKind.Relative), TestContext.Current.CancellationToken);

        var status = factory.Services.GetRequiredService<KeyRingStatus>();
        (status.Available, status.Path).ShouldBe((true, factory.KeyRingDir));
    }

    private ServiceProvider Build(string keyDir, string? backupDir = null)
    {
        Directory.CreateDirectory(ContentRoot);
        var configuration = Configuration((DataProtectionSetup.KeyRingDirKey, keyDir), (DataProtectionSetup.BackupDirKey, backupDir));
        return new ServiceCollection()
            .AddLogging()
            .AddClaudeKeyProtection(configuration, ContentRoot, new DataDirectory(DataDir))
            .BuildServiceProvider();
    }

    private static IDataProtector Protector(IServiceProvider services) =>
        services.GetRequiredService<IDataProtectionProvider>().CreateProtector(DataProtectionSetup.ClaudeKeyPurpose);

    private static IConfiguration Configuration(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values.Select(value => new KeyValuePair<string, string?>(value.Key, value.Value))).Build();

    private KeyRingLocations Locations(string keyDir) => new(keyDir, DataDir, null, ContentRoot);

    private sealed class FakeModes : IUnixFileModes
    {
        public static FakeModes Unsupported { get; } = new() { Supported = false };

        public bool Supported { get; init; } = true;

        public UnixFileMode Mode { get; set; }

        public bool RefuseSet { get; init; }

        public int SetCalls { get; private set; }

        public UnixFileMode Get(string path) => Mode;

        public void Set(string path, UnixFileMode mode)
        {
            SetCalls++;
            if (RefuseSet)
            {
                throw new UnauthorizedAccessException("not the owner");
            }

            Mode = mode;
        }
    }
}
