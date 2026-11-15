using Microsoft.Extensions.Options;
using StemStudio.Api.Worker;
using StemStudio.Domain.Scores;

namespace StemStudio.Api.Scores;

/// <summary>Seconds per audio second per engine and device, and the fixed overheads (data-model 4.5; configurable, refined in the integration wave).</summary>
public sealed class ScoreEstimateOptions
{
    /// <summary>basic-pitch (always on the CPU).</summary>
    public double BasicPitchCpu { get; set; } = 0.10;

    /// <summary>Piano CRNN on the CPU.</summary>
    public double PianoCrnnCpu { get; set; } = 1.7;

    /// <summary>Piano CRNN on the GPU (Phase 0: 0.075 s/s).</summary>
    public double PianoCrnnGpu { get; set; } = 0.08;

    /// <summary>Drum onsets (CPU).</summary>
    public double DrumOnsets { get; set; } = 0.02;

    /// <summary>Fixed overhead of every job.</summary>
    public double OverheadSeconds { get; set; } = 5;

    /// <summary>Extra seconds while the piano model is not downloaded yet.</summary>
    public double PianoDownloadSeconds { get; set; } = 60;
}

/// <summary>What the score features need from the worker: its device and whether the piano model is downloaded.</summary>
/// <param name="Gpu">True on a GPU worker.</param>
/// <param name="PianoReady">True when the piano model is downloaded.</param>
public sealed record ScoreEnvironment(bool Gpu, bool PianoReady);

/// <summary>Reads the <see cref="ScoreEnvironment"/> from the worker's <c>/health</c> and computes the estimates.</summary>
/// <param name="worker">Worker client.</param>
/// <param name="options">API options.</param>
public sealed class ScoreEstimator(IWorkerClient worker, IOptions<StemStudioApiOptions> options)
{
    /// <summary>The worker's environment; an unreachable worker counts as a CPU worker without the piano model.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The environment.</returns>
    public async Task<ScoreEnvironment> EnvironmentAsync(CancellationToken cancellationToken)
    {
        try
        {
            var health = await worker.GetHealthAsync(cancellationToken);
            return new ScoreEnvironment(health.DeviceInfo.Kind == Domain.Tracks.ProcessingDeviceKind.Gpu, health.Transcription?.PianoReady ?? false);
        }
        catch (Exception exception) when (exception is WorkerUnavailableException or WorkerRequestException)
        {
            return new ScoreEnvironment(false, false);
        }
    }

    /// <summary>Whole seconds of a transcription: <c>duration * factor(engine, device) + overhead</c>.</summary>
    /// <param name="durationSeconds">Duration of the source (or its range).</param>
    /// <param name="engine">The engine.</param>
    /// <param name="environment">The worker's environment.</param>
    /// <returns>Seconds.</returns>
    public int Seconds(double durationSeconds, ScoreEngine engine, ScoreEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        var factors = options.Value.ScoreEstimates;
        var download = engine == ScoreEngine.PianoCrnn && !environment.PianoReady ? factors.PianoDownloadSeconds : 0;
        return (int)Math.Round((durationSeconds * Factor(factors, engine, environment.Gpu)) + factors.OverheadSeconds + download, MidpointRounding.AwayFromZero);
    }

    private static double Factor(ScoreEstimateOptions factors, ScoreEngine engine, bool gpu) => engine switch
    {
        ScoreEngine.PianoCrnn => gpu ? factors.PianoCrnnGpu : factors.PianoCrnnCpu,
        ScoreEngine.DrumOnsets => factors.DrumOnsets,
        _ => factors.BasicPitchCpu,
    };
}
