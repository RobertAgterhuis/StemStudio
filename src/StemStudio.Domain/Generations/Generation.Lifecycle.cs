namespace StemStudio.Domain.Generations;

/// <summary>State transitions and take bookkeeping.</summary>
public sealed partial class Generation
{
    /// <summary>Records the generator job.</summary>
    /// <param name="generatorJobId">Job ID (32 lower-case hex characters).</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    public void AssignJob(string generatorJobId, DateTime nowUtc)
    {
        var jobId = JobIds.Require(generatorJobId, nameof(generatorJobId));
        RequireActive();
        (GeneratorJobId, UpdatedAt) = (jobId, nowUtc);
    }

    /// <summary>Applies a non-terminal generator report; ignored once terminal. Progress never drops within a status.</summary>
    /// <param name="report">The report.</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    /// <exception cref="ArgumentException">The report has a terminal status.</exception>
    public void ApplyReport(GenerationReport report, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(report);
        if (report.State is GenerationState.Completed or GenerationState.Failed or GenerationState.Cancelled)
        {
            throw new ArgumentException("Terminal states are applied with Complete, Fail or Cancel.", nameof(report));
        }

        if (IsTerminal)
        {
            return;
        }

        var progress = Math.Clamp(report.Progress, 0, 100);
        Progress = report.State == Status ? Math.Max(Progress, progress) : progress;
        (Status, Stage, Device, UpdatedAt) = (report.State, Text.TrimToLength(report.Stage, MaxStageLength) ?? Stage, report.Device ?? Device, nowUtc);
        _warnings = _warnings.Union(report.Warnings, StringComparer.Ordinal).Take(MaxWarnings).ToList();
    }

    /// <summary>Completes with the stored takes.</summary>
    /// <param name="takes">The takes (at least one).</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    public void Complete(IReadOnlyList<GenerationTake> takes, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(takes);
        if (takes.Count is < 1 or > MaxTakes)
        {
            throw new ArgumentException("A completed generation has one or two takes.", nameof(takes));
        }

        RequireActive();
        _takes = takes.OrderBy(take => take.Index).ToList();
        (Status, Progress, Stage, CompletedAt, UpdatedAt) = (GenerationState.Completed, 100, "Completed", nowUtc, nowUtc);
    }

    /// <summary>Fails an active generation.</summary>
    /// <param name="error">The reason (message cut to 500; blank becomes <see cref="FailedDefault"/>).</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    public void Fail(GenerationError error, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(error);
        RequireActive();
        var message = Text.TrimToLength(error.Message, MaxErrorLength) ?? FailedDefault;
        (Status, Stage, Error, UpdatedAt) = (GenerationState.Failed, "Failed", error with { Message = message }, nowUtc);
    }

    /// <summary>Cancels an active generation.</summary>
    /// <param name="nowUtc">Current time (UTC).</param>
    /// <returns>False when it was already terminal.</returns>
    public bool Cancel(DateTime nowUtc)
    {
        if (IsTerminal)
        {
            return false;
        }

        (Status, Stage, UpdatedAt) = (GenerationState.Cancelled, "Cancelled", nowUtc);
        return true;
    }

    /// <summary>Forgets the source track after it was deleted; the title snapshot stays.</summary>
    /// <param name="trackId">The deleted track.</param>
    /// <returns>True when this generation referenced it.</returns>
    public bool DetachSourceTrack(Guid trackId)
    {
        if (SourceTrackId != trackId)
        {
            return false;
        }

        SourceTrackId = null;
        Restyle = Restyle! with { SourceTrackId = null };
        return true;
    }

    /// <summary>The take with this number, if any.</summary>
    /// <param name="index">Take number from 1.</param>
    /// <returns>The take or null.</returns>
    public GenerationTake? Take(int index) => _takes.Find(take => take.Index == index);

    /// <summary>Records the library item of a take.</summary>
    /// <param name="index">Take number.</param>
    /// <param name="assetId">The item.</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    public void SetLibraryAsset(int index, Guid assetId, DateTime nowUtc) => Replace(index, take => take with { LibraryAssetId = assetId }, nowUtc);

    /// <summary>Records the track a take was imported as.</summary>
    /// <param name="index">Take number.</param>
    /// <param name="trackId">The track.</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    public void MarkImported(int index, Guid trackId, DateTime nowUtc) => Replace(index, take => take with { ImportedTrackId = trackId }, nowUtc);

    private void Replace(int index, Func<GenerationTake, GenerationTake> change, DateTime nowUtc)
    {
        var position = _takes.FindIndex(take => take.Index == index);
        if (position < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "No such take.");
        }

        _takes[position] = change(_takes[position]);
        UpdatedAt = nowUtc;
    }

    private void RequireActive()
    {
        if (IsTerminal)
        {
            throw new InvalidOperationException($"Generation {Id} is already {Status}.");
        }
    }
}
