using Microsoft.Extensions.Options;
using StemStudio.Api.Worker;
using StemStudio.Domain.InstrumentViews;
using StemStudio.Domain.Tracks;

namespace StemStudio.Api.InstrumentViews;

/// <summary>What the worker reports for the estimates: engines, device and BTC weights.</summary>
/// <param name="Engines">Configured engines.</param>
/// <param name="Gpu">The worker runs on a GPU.</param>
/// <param name="MissingModels">BTC models that are not downloaded yet.</param>
public sealed record WorkerSnapshot(WorkerEngines Engines, bool Gpu, IReadOnlySet<string> MissingModels)
{
    /// <summary>The snapshot when the health is unknown (defaults, CPU, weights present).</summary>
    public static WorkerSnapshot Unknown { get; } = new(WorkerEngines.Defaults, false, new HashSet<string>(StringComparer.Ordinal));
}

/// <summary>The shown estimates of data-model 7.7 from the worker health and <c>StemStudio:InstrumentViews:Estimates</c>.</summary>
/// <param name="worker">The worker client (health).</param>
/// <param name="options">Instrument view options.</param>
public sealed class AnalysisEstimator(IWorkerClient worker, IOptions<InstrumentViewsOptions> options)
{
    /// <summary>Model id of the simple vocabulary.</summary>
    public const string MajMinModel = "btc-majmin";

    /// <summary>Model id of the full vocabulary.</summary>
    public const string LargeVocabularyModel = "btc-large-voca";

    /// <summary>Reads the worker health; an unreachable or older worker gives <see cref="WorkerSnapshot.Unknown"/>.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The snapshot.</returns>
    public async Task<WorkerSnapshot> SnapshotAsync(CancellationToken cancellationToken)
    {
        try
        {
            return Snapshot(await worker.GetHealthAsync(cancellationToken));
        }
        catch (Exception exception) when (exception is WorkerUnavailableException or WorkerRequestException)
        {
            return WorkerSnapshot.Unknown;
        }
    }

    private static WorkerSnapshot Snapshot(WorkerHealth health)
    {
        var gpu = health.DeviceInfo.Kind == ProcessingDeviceKind.Gpu;
        if (health.InstrumentAnalysis is not { } lane)
        {
            return WorkerSnapshot.Unknown with { Gpu = gpu };
        }

        var missing = lane.Models.Where(model => model.Value != "ready").Select(model => model.Key).ToHashSet(StringComparer.Ordinal);
        return new WorkerSnapshot(new WorkerEngines(lane.ChordEngine, lane.PitchEngine), gpu, missing);
    }

    /// <summary>The estimate of a run in seconds.</summary>
    /// <param name="kind">The kind.</param>
    /// <param name="plan">The plan (engine).</param>
    /// <param name="durationSeconds">Track length.</param>
    /// <param name="snapshot">The worker snapshot.</param>
    /// <returns>Seconds.</returns>
    public int Estimate(AnalysisKind kind, AnalysisPlan plan, double durationSeconds, WorkerSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(snapshot);
        var factor = options.Value.Estimates.For(kind, plan.Engine, snapshot.Gpu);
        return AnalysisRules.EstimateSeconds(durationSeconds, factor, WeightsMissing(kind, plan, snapshot));
    }

    private static bool WeightsMissing(AnalysisKind kind, AnalysisPlan plan, WorkerSnapshot snapshot) =>
        kind == AnalysisKind.Chords && plan.Engine == "btc" && plan.Request is WorkerChordsRequest chords
        && snapshot.MissingModels.Contains(chords.Vocabulary == "full" ? LargeVocabularyModel : MajMinModel);
}
