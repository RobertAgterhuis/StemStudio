using Microsoft.Extensions.Options;
using StemStudio.Web.Audio;

namespace StemStudio.Web.Recording;

/// <summary>Web settings of play-along recording (configuration section <c>StemStudio:Recording</c>).</summary>
public sealed class RecordingUiOptions
{
    /// <summary>The configuration section.</summary>
    public const string Section = "StemStudio:Recording";

    /// <summary>
    /// E2E only: the engine feeds its own output into its input (<c>testInput: 'loopback'</c>, research R-13) instead of
    /// opening a microphone. Set by the e2e compose override (<c>StemStudio__Recording__TestLoopback=true</c>).
    /// </summary>
    public bool TestLoopback { get; set; }
}

/// <summary>Engine construction options of the recording feature: <c>{ testInput: 'loopback' }</c> when enabled.</summary>
/// <param name="options">The recording settings.</param>
public sealed class RecordingEngineOptions(IOptions<RecordingUiOptions> options) : IEngineOptionsSource
{
    /// <inheritdoc />
    public object? Options => options.Value.TestLoopback ? new LoopbackOptions("loopback") : null;

    private sealed record LoopbackOptions(string TestInput);
}

/// <summary>Service registration of the play-along recording UI (feature 015).</summary>
public static class RecordingUiRegistration
{
    /// <summary>Registers the recording UI services.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddRecordingUi(this IServiceCollection services)
    {
        services.AddOptions<RecordingUiOptions>().BindConfiguration(RecordingUiOptions.Section);
        services.AddScoped<IEngineOptionsSource, RecordingEngineOptions>();
        services.AddScoped<IRecordingJsHost>(sp => (AudioEngineInterop)sp.GetRequiredService<IAudioEngine>());
        services.AddScoped<IRecordingEngine, RecordingInterop>();
        services.AddScoped<RecordingPreferences>();
        return services;
    }
}
