using Microsoft.Extensions.Options;
using StemStudio.Api.Generator;
using StemStudio.Api.Sync;
using StemStudio.Domain.Generations;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Generations;

/// <summary>
/// One pass over the active generations (009 FR-007): mirrors the generator status, progress, stage, device and
/// warnings; collects the takes of completed jobs; maps failures and cancellations. An unreachable generator
/// changes nothing; a job the generator no longer knows fails as interrupted.
/// </summary>
/// <param name="generations">Repository.</param>
/// <param name="router">Picks the generator or the singer.</param>
/// <param name="collector">Take collector.</param>
/// <param name="clock">Clock.</param>
/// <param name="waiter">Submits voice conversions once their stems are ready.</param>
public sealed class GenerationSynchronizer(
    IGenerationRepository generations, GeneratorRouter router, TakeCollector collector, Voices.ConversionStemWaiter waiter, TimeProvider clock)
{
    /// <summary>Error text when the generator lost a job.</summary>
    public const string Lost = "The generator lost this generation (it was restarted).";

    /// <summary>Synchronises every active generation that has a generator job.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    public async Task SyncAllAsync(CancellationToken cancellationToken)
    {
        foreach (var generation in await generations.ListActiveAsync(cancellationToken))
        {
            await (generation.Status == GenerationState.WaitingForStems ? AdvanceAsync(generation, cancellationToken) : SyncAsync(generation, cancellationToken));
        }
    }

    private async Task AdvanceAsync(Generation conversion, CancellationToken cancellationToken)
    {
        await waiter.AdvanceAsync(conversion, cancellationToken);
        await generations.SaveAsync(cancellationToken);
    }

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    private async Task SyncAsync(Generation generation, CancellationToken cancellationToken)
    {
        if (generation.GeneratorJobId is null)
        {
            return;
        }

        GeneratorJob? job;
        try
        {
            job = await router.For(generation).GetAsync(generation.GeneratorJobId, cancellationToken);
        }
        catch (GeneratorUnavailableException)
        {
            return;
        }

        await ApplyAsync(generation, job, cancellationToken);
        await generations.SaveAsync(cancellationToken);
    }

    private Task ApplyAsync(Generation generation, GeneratorJob? job, CancellationToken cancellationToken)
    {
        switch (job?.Status)
        {
            case null:
                generation.Fail(new GenerationError(GenerationErrorCode.Interrupted, Lost), Now);
                break;
            case GeneratorJobStatus.Completed:
                return collector.CollectAsync(generation, job, cancellationToken);
            case GeneratorJobStatus.Failed:
                generation.Fail(Error(job.Error), Now);
                break;
            case GeneratorJobStatus.Cancelled:
                generation.Cancel(Now);
                break;
            default:
                generation.ApplyReport(Report(job), Now);
                break;
        }

        return Task.CompletedTask;
    }

    private static GenerationReport Report(GeneratorJob job) =>
        new(Enum.Parse<GenerationState>(job.Status.ToString()), job.Progress, job.Stage, Device(job.Device), job.Warnings);

    private static JobDevice? Device(GeneratorJobDevice? device) => device is null
        ? null
        : new JobDevice(device.Kind == "gpu" ? ProcessingDeviceKind.Gpu : ProcessingDeviceKind.Cpu, device.Kind == "gpu" ? device.Name : null, false, null);

    private static GenerationError Error(GeneratorJobError? error) =>
        new(Enum.TryParse<GenerationErrorCode>(error?.Code, true, out var code) && Enum.IsDefined(code) ? code : GenerationErrorCode.EngineError, error?.Message ?? string.Empty);
}

/// <summary>Background poller of the generations: one <see cref="GenerationSynchronizer"/> pass every <see cref="StemStudioApiOptions.GenerationPollInterval"/> (2 s).</summary>
/// <param name="scopes">Scope factory.</param>
/// <param name="options">API options.</param>
/// <param name="clock">Clock driving the timer.</param>
/// <param name="logger">Logger.</param>
public sealed class GenerationSyncService(
    IServiceScopeFactory scopes, IOptions<StemStudioApiOptions> options, TimeProvider clock, ILogger<GenerationSyncService> logger) : PeriodicSyncService(scopes, clock, logger)
{
    /// <inheritdoc />
    protected override TimeSpan Interval => options.Value.GenerationPollInterval;

    /// <inheritdoc />
    protected override Task PassAsync(IServiceProvider services, CancellationToken cancellationToken) =>
        services.GetRequiredService<GenerationSynchronizer>().SyncAllAsync(cancellationToken);
}
