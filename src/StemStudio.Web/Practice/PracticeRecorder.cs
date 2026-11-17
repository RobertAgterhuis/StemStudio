using StemStudio.Contracts;
using StemStudio.Contracts.Practice;
using StemStudio.Contracts.Sections;
using StemStudio.Web.Api;
using StemStudio.Web.Audio;

namespace StemStudio.Web.Practice;

/// <summary>
/// Records practice sessions from the mixer's playback (014 data-model 5.4; FR-025, FR-029). <c>Idle</c> -> (playing)
/// <c>Running</c> -> (paused/stopped) <c>Holding</c> -> (playing within 10 min) <c>Running</c>, or a new session after
/// 10 min; page leave or track change -> <c>Closed</c>. Each position report adds the wall-clock time since the last one
/// (<see cref="TimeProvider"/>) to the session and to the section of the last position. Flushes (cumulative upserts):
/// every 30 s while running, on holding and on close. A 409 <c>practice-log-off</c> stops the recorder; other errors keep
/// the values for the next flush (at most 30 s are lost on a crash).
/// </summary>
public sealed class PracticeRecorder
{
    /// <summary>Flush interval while running.</summary>
    public static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(30);

    /// <summary>A pause longer than this ends the session.</summary>
    public static readonly TimeSpan HoldLimit = TimeSpan.FromMinutes(10);

    private const double PassTolerance = 0.05;

    private readonly Func<Guid, PracticeSessionInputDto, CancellationToken, Task> _write;
    private readonly TimeProvider _time;
    private IReadOnlyList<SectionDto> _sections = [];
    private Guid _trackId;
    private Guid? _mixId;
    private PracticeTally? _session;
    private DateTimeOffset _lastTick;
    private DateTimeOffset _lastFlush;
    private DateTimeOffset _holdSince;
    private double _lastPosition;
    private bool _flushing;

    /// <summary>Creates a recorder.</summary>
    /// <param name="write">Upserts a session (<c>PUT /api/practice/sessions/{id}</c>).</param>
    /// <param name="time">Clock.</param>
    public PracticeRecorder(Func<Guid, PracticeSessionInputDto, CancellationToken, Task> write, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(write);
        ArgumentNullException.ThrowIfNull(time);
        _write = write;
        _time = time;
    }

    /// <summary>Current state.</summary>
    public PracticeRecorderState State { get; private set; }

    /// <summary>Id of the current or last session, or null before the first.</summary>
    public Guid? SessionId => _session?.Id;

    /// <summary>Starts a track context (state <c>Idle</c>; any previous session is dropped, close it first).</summary>
    /// <param name="trackId">Track.</param>
    /// <param name="mixId">Loaded mix, or null.</param>
    /// <param name="sections">The track's sections.</param>
    public void Begin(Guid trackId, Guid? mixId, IReadOnlyList<SectionDto> sections)
    {
        ArgumentNullException.ThrowIfNull(sections);
        (_trackId, _mixId, _sections, _session, State) = (trackId, mixId, sections, null, PracticeRecorderState.Idle);
    }

    /// <summary>Closes the session of the current track and starts the context of another one.</summary>
    /// <param name="trackId">Track.</param>
    /// <param name="mixId">Loaded mix, or null.</param>
    /// <param name="sections">The track's sections.</param>
    /// <returns>A task that completes after the final flush.</returns>
    public async Task ChangeTrackAsync(Guid trackId, Guid? mixId, IReadOnlyList<SectionDto> sections)
    {
        await CloseAsync();
        Begin(trackId, mixId, sections);
    }

    /// <summary>Replaces the sections (after detection or an edit).</summary>
    /// <param name="sections">The sections.</param>
    public void UpdateSections(IReadOnlyList<SectionDto> sections)
    {
        ArgumentNullException.ThrowIfNull(sections);
        _sections = sections;
    }

    /// <summary>Sets the loaded mix.</summary>
    /// <param name="mixId">Mix, or null.</param>
    public void SetMix(Guid? mixId) => _mixId = mixId;

    /// <summary>Playback state change of the engine.</summary>
    /// <param name="state">New state.</param>
    /// <param name="position">Position in seconds.</param>
    /// <returns>A task that completes after a flush on holding.</returns>
    public Task OnPlaybackAsync(PlaybackState state, double position)
    {
        if (State == PracticeRecorderState.Off)
        {
            return Task.CompletedTask;
        }

        if (state == PlaybackState.Playing)
        {
            Run(position);
            return Task.CompletedTask;
        }

        return HoldAsync();
    }

    /// <summary>Position report while playing: adds the elapsed time and flushes every 30 s.</summary>
    /// <param name="position">Position in seconds.</param>
    /// <param name="speedPercent">Playback speed heard.</param>
    /// <returns>A task that completes after a due flush.</returns>
    public Task OnPositionAsync(double position, double speedPercent)
    {
        if (State != PracticeRecorderState.Running)
        {
            return Task.CompletedTask;
        }

        Account();
        _lastPosition = position;
        _session!.Speed(speedPercent);
        return _time.GetUtcNow() - _lastFlush >= FlushInterval ? FlushAsync() : Task.CompletedTask;
    }

    /// <summary>A completed loop pass (engine <c>OnLoopPass</c>).</summary>
    /// <param name="pass">The pass.</param>
    /// <param name="trainerOn">The 012 speed trainer was running.</param>
    /// <param name="loopSectionId">The loop's section (the loop came from "loop this section"), or null.</param>
    public void OnLoopPass(LoopPassInfo pass, bool trainerOn, Guid? loopSectionId)
    {
        ArgumentNullException.ThrowIfNull(pass);
        if (_session is null || State is not (PracticeRecorderState.Running or PracticeRecorderState.Holding))
        {
            return;
        }

        var section = LoopSection(pass, loopSectionId);
        _session.AddPass(section, trainerOn && loopSectionId is not null && Covers(pass, section) ? pass.SpeedPercent : null);
    }

    /// <summary>Ends the session (page leave, track change) with a final flush.</summary>
    /// <returns>A task that completes after the flush.</returns>
    public async Task CloseAsync()
    {
        if (State is PracticeRecorderState.Off or PracticeRecorderState.Idle or PracticeRecorderState.Closed)
        {
            return;
        }

        if (State == PracticeRecorderState.Running)
        {
            Account();
        }

        await FlushAsync();
        State = State == PracticeRecorderState.Off ? State : PracticeRecorderState.Closed;
    }

    private static bool Covers(LoopPassInfo pass, SectionDto? section) =>
        section is not null && Math.Abs(pass.Start - section.StartSeconds) <= PassTolerance && Math.Abs(pass.End - section.EndSeconds) <= PassTolerance;

    private SectionDto? LoopSection(LoopPassInfo pass, Guid? loopSectionId) => loopSectionId is { } id
        ? _sections.FirstOrDefault(s => s.Id == id)
        : SectionNavigator.Current(_sections, (pass.Start + pass.End) / 2);

    private void Run(double position)
    {
        if (State == PracticeRecorderState.Running)
        {
            return;
        }

        var now = _time.GetUtcNow();
        if (_session is null || State != PracticeRecorderState.Holding || now - _holdSince >= HoldLimit)
        {
            _session = new PracticeTally(Guid.NewGuid(), now);
            _lastFlush = now;
        }

        (_lastTick, _lastPosition, State) = (now, position, PracticeRecorderState.Running);
    }

    private Task HoldAsync()
    {
        if (State != PracticeRecorderState.Running)
        {
            return Task.CompletedTask;
        }

        Account();
        (_holdSince, State) = (_time.GetUtcNow(), PracticeRecorderState.Holding);
        return FlushAsync();
    }

    private void Account()
    {
        var now = _time.GetUtcNow();
        _session!.AddTime((now - _lastTick).TotalSeconds, SectionNavigator.Current(_sections, _lastPosition));
        _lastTick = now;
    }

    private async Task FlushAsync()
    {
        if (_flushing || _session is not { PlayedSeconds: > 0 } session)
        {
            return;
        }

        _flushing = true;
        _lastFlush = _time.GetUtcNow();
        try
        {
            await _write(session.Id, session.ToInput(_trackId, _mixId, _lastFlush), CancellationToken.None);
        }
        catch (ApiException ex) when (ex.Error.IsConflict && ex.Error.Code == PracticeFlowProblemTypes.PracticeLogOff)
        {
            State = PracticeRecorderState.Off;
        }
        catch (Exception ex) when (ex is ApiException or HttpRequestException or OperationCanceledException)
        {
            // Kept: the values are cumulative, the next flush sends them again.
        }
        finally
        {
            _flushing = false;
        }
    }
}
