using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StemStudio.Agent.Backends;
using StemStudio.Agent.Ports;
using StemStudio.Api.Security;
using StemStudio.Api.Storage;

namespace StemStudio.Api.Agent.Secrets;

/// <summary>
/// ASP.NET Core Data Protection of the API (011 follow-up claude-key, plan "Configuration of Data Protection", CK-FR-002..005):
/// fixed application name, key ring persisted in its own directory (<c>/keys</c> in Docker, a per-user directory otherwise),
/// DPAPI for the current user on Windows. Default key lifetime and algorithms are unchanged and keys are never deleted. When the
/// directory is unusable the API still starts with an ephemeral provider and key storage disabled. This folder is the only
/// place that references Data Protection (quality-gate rule).
/// </summary>
public static class DataProtectionSetup
{
    /// <summary>Fixed application name (discriminator): ciphertexts stay valid across rebuilds and content-root changes.</summary>
    public const string ApplicationName = "StemStudio.Api";

    /// <summary>Fixed protector purpose of the stored Claude key (version 1).</summary>
    public const string ClaudeKeyPurpose = "StemStudio.Agent.ClaudeApiKey.v1";

    /// <summary>Configuration key of the key ring directory (environment <c>StemStudio__DataProtection__KeyRingDir</c>).</summary>
    public const string KeyRingDirKey = "StemStudio:DataProtection:KeyRingDir";

    /// <summary>Configuration key of the Web app's browser origin (the only accepted <c>Origin</c> of a key write).</summary>
    public const string WebOriginKey = "StemStudio:WebOrigin";

    /// <summary>Configuration key of the 013 backups folder, which the key ring must stay out of.</summary>
    public const string BackupDirKey = "StemStudio:BackupDir";

    /// <summary>Registers Data Protection and the <see cref="KeyRingStatus"/> singleton.</summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">Configuration.</param>
    /// <param name="contentRoot">Content root of the API (relative paths resolve against it; the ring must stay out of it).</param>
    /// <param name="dataDirectory">The library.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddClaudeKeyProtection(
        this IServiceCollection services, IConfiguration configuration, string contentRoot, DataDirectory dataDirectory)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(dataDirectory);
        var backupDir = Resolve(configuration[BackupDirKey], contentRoot);
        var locations = new KeyRingLocations(ConfiguredKeyRingDirectory(configuration, contentRoot), dataDirectory.Root, backupDir, contentRoot);
        var status = KeyRingDirectory.Prepare(locations, UnixFileModes.ForCurrentPlatform());
        services.AddSingleton(status);
        Configure(services.AddDataProtection().SetApplicationName(ApplicationName), status);
        return services;
    }

    /// <summary>
    /// Registers <see cref="ProtectedClaudeKeyStore"/> as the singleton behind <see cref="IStoredClaudeKeyReader"/> and
    /// <see cref="IStoredClaudeKeyWriter"/>, the agent library's <see cref="ClaudeKeyProvider"/> unless one is registered, and
    /// the HTTP logging interceptor of the key route (needs <see cref="AddClaudeKeyProtection"/> and the persistence registrations).
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">Configuration (internal caller and web origin of the local-only rule).</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddClaudeKeyStore(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddSingleton(new KeyEntryPolicy(configuration[KeyEntryPolicy.InternalCallerKey], configuration[WebOriginKey] ?? new StemStudioApiOptions().WebOrigin));
        services.TryAddSingleton<IHostAddressResolver, DnsHostAddressResolver>();
        services.AddSingleton<TrustedPeerPolicy>();
        services.AddSingleton<LocalOnlyKeyEntryFilter>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ClaudeKeyProtector>();
        services.AddSingleton<ClaudeKeyRow>();
        services.AddSingleton<ProtectedClaudeKeyStore>();
        services.AddSingleton<IStoredClaudeKeyReader>(sp => sp.GetRequiredService<ProtectedClaudeKeyStore>());
        services.AddSingleton<IStoredClaudeKeyWriter>(sp => sp.GetRequiredService<ProtectedClaudeKeyStore>());
        services.TryAddSingleton<IClaudeKeyProvider, ClaudeKeyProvider>();
        services.AddSingleton<ClaudeKeyStoreServices>();
        services.AddHttpLoggingInterceptor<KeyEndpointLoggingInterceptor>();
        return services;
    }

    /// <summary>The configured key ring directory (relative to the content root), or the per-user default.</summary>
    /// <param name="configuration">Configuration.</param>
    /// <param name="contentRoot">Content root.</param>
    /// <returns>An absolute path.</returns>
    public static string ConfiguredKeyRingDirectory(IConfiguration configuration, string contentRoot)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return Resolve(configuration[KeyRingDirKey], contentRoot) ?? KeyRingDefaults.ForCurrentProcess();
    }

    private static void Configure(IDataProtectionBuilder builder, KeyRingStatus status)
    {
        if (!status.Available)
        {
            builder.UseEphemeralDataProtectionProvider();
            return;
        }

        builder.PersistKeysToFileSystem(new DirectoryInfo(status.Path));
        if (OperatingSystem.IsWindows())
        {
            builder.ProtectKeysWithDpapi();
        }
    }

    private static string? Resolve(string? configured, string contentRoot) =>
        string.IsNullOrWhiteSpace(configured) ? null : Path.GetFullPath(Path.Combine(contentRoot, configured));
}
