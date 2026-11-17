using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StemStudio.Agent.Backends;
using StemStudio.Agent.Ports;

namespace StemStudio.Agent.LyricAssist;

/// <summary>Registration of the lyric assistant's model layer in the API host (CTO decision on the ai-agent / backend-023 seam).</summary>
public static class LyricAssistServiceCollectionExtensions
{
    /// <summary>Claude input token cap per assist.</summary>
    public const string MaxInputTokensVariable = "StemStudio__LyricAssist__MaxInputTokensPerAssist";

    /// <summary>Claude output token cap per assist.</summary>
    public const string MaxOutputTokensVariable = "StemStudio__LyricAssist__MaxOutputTokensPerAssist";

    /// <summary>Block timeout in seconds (local model; Claude gets 1.5 times it).</summary>
    public const string BlockTimeoutVariable = "StemStudio__LyricAssist__BlockTimeoutSeconds";

    /// <summary>
    /// Registers <see cref="ILyricAssistantFactory"/>, <see cref="LyricModelClient"/>, <see cref="LyricAssistOptions"/> (from the
    /// environment) and the shared model clients (environment, clock, GPU state, Ollama, Claude key provider and factory), all with
    /// <c>TryAdd</c>. The host registers <see cref="ILyricVerification"/> (backend adapter over the Domain services) and
    /// <see cref="IAgentSettingsStore"/>; <see cref="ILyricAssistant"/> instances come from the factory, one per assist.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddLyricAssistAgent(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        AgentServiceCollectionExtensions.AddModelBackends(services);
        services.TryAddSingleton(sp => Options(sp.GetRequiredService<IEnvironmentReader>()));
        services.TryAddSingleton(sp => new LyricModelClient(
            sp.GetRequiredService<IAgentSettingsStore>(),
            sp.GetRequiredService<IGpuActivity>(),
            new LyricModelBackends { Ollama = sp.GetRequiredService<OllamaChatClientFactory>(), Claude = sp.GetRequiredService<ClaudeChatClientFactory>() }));
        services.TryAddSingleton<ILyricAssistantFactory>(sp => new LyricAssistantFactory(new LyricAssistantServices
        {
            Settings = sp.GetRequiredService<IAgentSettingsStore>(),
            ClaudeKeys = sp.GetRequiredService<IClaudeKeyProvider>(),
            Model = sp.GetRequiredService<LyricModelClient>(),
            Verification = sp.GetRequiredService<ILyricVerification>(),
            Environment = sp.GetRequiredService<IEnvironmentReader>(),
            Options = sp.GetRequiredService<LyricAssistOptions>(),
            Time = sp.GetRequiredService<TimeProvider>(),
        }));
        return services;
    }

    /// <summary>The limits from the environment; missing or invalid values keep the defaults.</summary>
    /// <param name="environment">Environment.</param>
    /// <returns>The options.</returns>
    public static LyricAssistOptions Options(IEnvironmentReader environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        var defaults = new LyricAssistOptions();
        var timeout = Positive(environment, BlockTimeoutVariable);
        return defaults with
        {
            MaxInputTokensPerAssist = Positive(environment, MaxInputTokensVariable) ?? defaults.MaxInputTokensPerAssist,
            MaxOutputTokensPerAssist = Positive(environment, MaxOutputTokensVariable) ?? defaults.MaxOutputTokensPerAssist,
            LocalBlockTimeout = timeout is { } seconds ? TimeSpan.FromSeconds(seconds) : defaults.LocalBlockTimeout,
            ClaudeBlockTimeout = timeout is { } claude ? TimeSpan.FromSeconds(claude * 1.5) : defaults.ClaudeBlockTimeout,
        };
    }

    private static int? Positive(IEnvironmentReader environment, string name) =>
        int.TryParse(environment.GetVariable(name), NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value > 0 ? value : null;
}
