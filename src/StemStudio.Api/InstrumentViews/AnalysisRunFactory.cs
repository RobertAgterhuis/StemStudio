using StemStudio.Api.Worker;
using StemStudio.Domain.InstrumentViews;
using StemStudio.Domain.Tracks;

namespace StemStudio.Api.InstrumentViews;

/// <summary>A start request.</summary>
/// <param name="Track">The track.</param>
/// <param name="Kind">The kind.</param>
/// <param name="Options">Run options.</param>
/// <param name="OverwriteEdits">The user confirmed replacing edited chords.</param>
/// <param name="Automatic">Started by an auto rule (a missing prerequisite is stored instead of refused).</param>
public sealed record StartRequest(Track Track, AnalysisKind Kind, AnalysisOptions Options, bool OverwriteEdits, bool Automatic = false);

/// <summary>A run ready to submit.</summary>
/// <param name="Request">The worker request.</param>
/// <param name="Options">The run options.</param>
/// <param name="Fingerprint">The input fingerprint.</param>
/// <param name="EstimateSeconds">The shown estimate.</param>
public sealed record PreparedRun(WorkerAnalysisRequest Request, AnalysisOptions Options, string Fingerprint, int EstimateSeconds)
{
    /// <summary>The domain run of the job the worker created.</summary>
    /// <param name="jobId">The worker job id.</param>
    /// <returns>The run.</returns>
    public AnalysisRun ToRun(string jobId) => new(jobId, Options, Fingerprint, EstimateSeconds);
}

/// <summary>A planned run, or the missing prerequisite.</summary>
/// <param name="Missing">The missing prerequisite.</param>
/// <param name="Run">The prepared run, or null.</param>
public sealed record PlannedRun(MissingPrerequisite Missing, PreparedRun? Run);

/// <summary>Plans a run: inputs (data-model 7.4), fingerprint (7.1) and estimate (7.7).</summary>
/// <param name="fingerprinter">Hashes the input stems.</param>
/// <param name="estimator">Estimates from the worker health.</param>
public sealed class AnalysisRunFactory(StemFingerprinter fingerprinter, AnalysisEstimator estimator)
{
    /// <summary>Plans a run.</summary>
    /// <param name="request">The start request.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The planned run.</returns>
    public async Task<PlannedRun> PlanAsync(StartRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var snapshot = await estimator.SnapshotAsync(cancellationToken);
        var result = AnalysisKindStrategies.Plan(request.Kind, request.Track, request.Options, snapshot.Engines);
        if (result.Plan is not { } plan)
        {
            return new PlannedRun(result.Missing, null);
        }

        var estimate = estimator.Estimate(request.Kind, plan, request.Track.DurationSeconds ?? 0, snapshot);
        return new PlannedRun(MissingPrerequisite.None, new PreparedRun(plan.Request, request.Options, await FingerprintAsync(request.Track.Id, plan, cancellationToken), estimate));
    }

    /// <summary>The fingerprint of a plan's inputs.</summary>
    /// <param name="trackId">The track.</param>
    /// <param name="plan">The plan.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The fingerprint.</returns>
    public Task<string> FingerprintAsync(Guid trackId, AnalysisPlan plan, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return fingerprinter.FingerprintAsync(trackId, plan.Inputs, cancellationToken);
    }
}
