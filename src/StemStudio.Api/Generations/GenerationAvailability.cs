using StemStudio.Api.Generator;
using StemStudio.Contracts.Generation;

namespace StemStudio.Api.Generations;

/// <summary>Whether the generator can take jobs, with its health when it answered.</summary>
/// <param name="State">Not installed, unavailable or ready.</param>
/// <param name="Reason">Readable reason when not ready.</param>
/// <param name="Health">The generator health, when it answered.</param>
public sealed record GenerationReadiness(GenerationAvailabilityDto State, string? Reason, GeneratorHealth? Health)
{
    /// <summary>True when generation is possible.</summary>
    public bool IsReady => State == GenerationAvailabilityDto.Ready;
}

/// <summary>
/// The generator's availability (backend <c>getGenerationStatus</c>): not installed without a base URL or when
/// <c>/health</c> does not answer within 2 s, unavailable with the generator's reason without a usable GPU, else
/// ready. Cached for 10 seconds.
/// </summary>
/// <param name="generator">The generator client.</param>
/// <param name="clock">Clock for the cache.</param>
public class GenerationAvailability(IGeneratorClient generator, TimeProvider clock)
{
    /// <summary>The reason given when the service is not installed or not reachable.</summary>
    protected virtual string NotInstalled => NotInstalledReason;

    /// <summary>How long a probe is reused.</summary>
    public static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(10);

    /// <summary>Reason when the generator is not installed or not reachable.</summary>
    public const string NotInstalledReason = "The generator service is not installed.";

    private readonly Lock _lock = new();
    private (DateTimeOffset At, GenerationReadiness Value)? _cached;

    /// <summary>The cached or a fresh probe.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The readiness.</returns>
    public async Task<GenerationReadiness> GetAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        lock (_lock)
        {
            if (_cached is { } cached && now - cached.At < CacheDuration)
            {
                return cached.Value;
            }
        }

        var fresh = await ProbeAsync(cancellationToken);
        lock (_lock)
        {
            _cached = (now, fresh);
        }

        return fresh;
    }

    /// <summary>Forgets the cached probe (e.g. after the generator refused a job for the GPU).</summary>
    public void Invalidate()
    {
        lock (_lock)
        {
            _cached = null;
        }
    }

    private async Task<GenerationReadiness> ProbeAsync(CancellationToken cancellationToken)
    {
        if (!generator.IsConfigured)
        {
            return new GenerationReadiness(GenerationAvailabilityDto.NotInstalled, NotInstalled, null);
        }

        try
        {
            var health = await generator.GetHealthAsync(cancellationToken);
            return health.Ready
                ? new GenerationReadiness(GenerationAvailabilityDto.Ready, null, health)
                : new GenerationReadiness(GenerationAvailabilityDto.Unavailable, health.Reason ?? health.Device.Reason ?? "The generator has no usable GPU.", health);
        }
        catch (GeneratorUnavailableException)
        {
            return new GenerationReadiness(GenerationAvailabilityDto.NotInstalled, NotInstalled, null);
        }
    }
}

/// <summary>The singer's availability (010: voice profiles and conversions), cached like the generator's.</summary>
/// <param name="singer">The singer client.</param>
/// <param name="clock">Clock.</param>
public sealed class SingerAvailability(ISingerClient singer, TimeProvider clock) : GenerationAvailability(singer, clock)
{
    /// <summary>Reason when the singer is not installed or not reachable.</summary>
    public const string SingerNotInstalledReason = "The singer service is not installed.";

    /// <inheritdoc />
    protected override string NotInstalled => SingerNotInstalledReason;
}

/// <summary>Random seeds for takes (replaced in tests).</summary>
public interface ISeedSource
{
    /// <summary>A seed in 0..2^31-2.</summary>
    /// <returns>The seed.</returns>
    int NewSeed();
}

/// <summary>Seeds from the cryptographic random generator.</summary>
public sealed class RandomSeedSource : ISeedSource
{
    /// <inheritdoc />
    public int NewSeed() => System.Security.Cryptography.RandomNumberGenerator.GetInt32(0, int.MaxValue);
}
