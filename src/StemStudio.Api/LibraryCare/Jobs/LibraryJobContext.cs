using StemStudio.Contracts.LibraryCare;

namespace StemStudio.Api.LibraryCare.Jobs;

/// <summary>
/// Progress reporting of one running library job: keeps the live <see cref="LibraryJobDto"/>, computes the ETA from the
/// moving average of the last 10 seconds (null in the first 5 seconds, data-model 4.2).
/// </summary>
public sealed class LibraryJobContext
{
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan Warmup = TimeSpan.FromSeconds(5);

    private readonly Lock _lock = new();
    private readonly Queue<(DateTimeOffset At, long Bytes)> _samples = new();
    private readonly TimeProvider _clock;
    private readonly DateTimeOffset _startedAt;
    private LibraryJobDto _job;

    /// <summary>Creates the context of a started job.</summary>
    /// <param name="job">The running job.</param>
    /// <param name="clock">Clock.</param>
    public LibraryJobContext(LibraryJobDto job, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(job);
        (_job, _clock, _startedAt) = (job, clock, job.StartedAt ?? clock.GetUtcNow());
    }

    /// <summary>The job id.</summary>
    public Guid JobId => _job.JobId;

    /// <summary>The live job.</summary>
    public LibraryJobDto Job
    {
        get
        {
            lock (_lock)
            {
                return _job;
            }
        }
    }

    /// <summary>Applies a progress report.</summary>
    /// <param name="progress">The report.</param>
    public void Report(LibraryJobProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);
        var now = _clock.GetUtcNow();
        lock (_lock)
        {
            _samples.Enqueue((now, progress.BytesDone));
            while (_samples.Count > 2 && now - _samples.Peek().At > Window)
            {
                _samples.Dequeue();
            }

            _job = _job with
            {
                Stage = progress.Stage,
                StageText = progress.StageText,
                Progress = Math.Clamp(progress.Progress, 0, 100),
                BytesDone = progress.BytesDone,
                BytesTotal = progress.BytesTotal,
                CurrentItem = progress.CurrentItem,
                EtaSeconds = Eta(now, progress),
            };
        }
    }

    /// <summary>Marks the job as no longer cancellable (a restore closes the gate).</summary>
    public void MarkNotCancellable() => Update(job => job with { Cancellable = false });

    /// <summary>Replaces the live job.</summary>
    /// <param name="change">The change.</param>
    public void Update(Func<LibraryJobDto, LibraryJobDto> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        lock (_lock)
        {
            _job = change(_job);
        }
    }

    private int? Eta(DateTimeOffset now, LibraryJobProgress progress)
    {
        var (oldestAt, oldestBytes) = _samples.Peek();
        var seconds = (now - oldestAt).TotalSeconds;
        if (now - _startedAt < Warmup || seconds <= 0 || progress.BytesDone <= oldestBytes)
        {
            return null;
        }

        var rate = (progress.BytesDone - oldestBytes) / seconds;
        return (int)Math.Round(Math.Max(0, progress.BytesTotal - progress.BytesDone) / rate);
    }
}
