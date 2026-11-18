using System.Net;
using Microsoft.Extensions.Time.Testing;
using StemStudio.Contracts;
using StemStudio.Contracts.Practice;
using StemStudio.Contracts.Sections;
using StemStudio.Web.Api;
using StemStudio.Web.Audio;
using StemStudio.Web.Practice;

namespace StemStudio.Web.Tests.Practice;

/// <summary>T048: practice recorder states, per-section split, loop passes, trainer percent, flushes and errors (014 data-model 5.4; FR-025, FR-029, SC-007).</summary>
public sealed class PracticeRecorderTests
{
    private static readonly Guid TrackId = Guid.NewGuid();
    private static readonly Guid MixId = Guid.NewGuid();
    private static readonly SectionDto Intro = SectionNavigatorTests.Section(SectionKindDto.Intro, "Intro", 0, 10);
    private static readonly SectionDto Verse = SectionNavigatorTests.Section(SectionKindDto.Verse, "Verse 1", 10, 30);

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 17, 9, 0, 0, TimeSpan.Zero));
    private readonly List<(Guid Id, PracticeSessionInputDto Session)> _writes = [];
    private readonly PracticeRecorder _recorder;
    private Func<Exception?> _failure = () => null;

    public PracticeRecorderTests()
    {
        _recorder = new PracticeRecorder(WriteAsync, _time);
        _recorder.Begin(TrackId, MixId, [Intro, Verse]);
    }

    private PracticeSessionInputDto Last => _writes[^1].Session;

    private Task WriteAsync(Guid id, PracticeSessionInputDto session, CancellationToken cancellationToken)
    {
        if (_failure() is { } error)
        {
            return Task.FromException(error);
        }

        _writes.Add((id, session));
        return Task.CompletedTask;
    }

    private async Task PlayAsync(double from, double seconds, double step = 0.25, double speed = 100)
    {
        for (var t = 0.0; t < seconds - 1e-9; t += step)
        {
            _time.Advance(TimeSpan.FromSeconds(step));
            await _recorder.OnPositionAsync(from + t + step, speed);
        }
    }

    [Fact]
    public async Task StartsIdle_AndRunsOnPlaying()
    {
        _recorder.State.ShouldBe(PracticeRecorderState.Idle);

        await _recorder.OnPlaybackAsync(PlaybackState.Playing, 0);

        _recorder.State.ShouldBe(PracticeRecorderState.Running);
        _recorder.SessionId.ShouldNotBeNull();
    }

    [Fact]
    public async Task Pause_HoldsAndFlushes_PlayingTimeOnly()
    {
        await _recorder.OnPlaybackAsync(PlaybackState.Playing, 0);
        await PlayAsync(0, 5);
        _time.Advance(TimeSpan.FromSeconds(2));
        await _recorder.OnPlaybackAsync(PlaybackState.Paused, 5);

        _recorder.State.ShouldBe(PracticeRecorderState.Holding);
        Last.PlayedSeconds.ShouldBe(7, 0.001);
        _time.Advance(TimeSpan.FromMinutes(3));
        await _recorder.OnPlaybackAsync(PlaybackState.Playing, 5);
        await PlayAsync(5, 4);
        await _recorder.OnPlaybackAsync(PlaybackState.Stopped, 0);

        _writes.Select(w => w.Id).Distinct().Count().ShouldBe(1);
        Last.PlayedSeconds.ShouldBe(11, 0.001);
        (Last.TrackId, Last.MixId).ShouldBe((TrackId, MixId));
        Last.StartedAt.ShouldBe(new DateTimeOffset(2026, 11, 17, 9, 0, 0, TimeSpan.Zero));
        Last.EndedAt.ShouldBe(_time.GetUtcNow());
    }

    [Fact]
    public async Task PlayingAfterTenMinutesHolding_StartsANewSession()
    {
        await _recorder.OnPlaybackAsync(PlaybackState.Playing, 0);
        await PlayAsync(0, 2);
        await _recorder.OnPlaybackAsync(PlaybackState.Paused, 2);
        var first = _recorder.SessionId;
        _time.Advance(TimeSpan.FromMinutes(10));

        await _recorder.OnPlaybackAsync(PlaybackState.Playing, 2);
        await PlayAsync(2, 3);
        await _recorder.CloseAsync();

        _recorder.SessionId.ShouldNotBe(first);
        _writes.Last(w => w.Id == first).Session.PlayedSeconds.ShouldBe(2, 0.001);
        Last.PlayedSeconds.ShouldBe(3, 0.001);
        _recorder.State.ShouldBe(PracticeRecorderState.Closed);
    }

    [Fact]
    public async Task SplitsTimePerSection_FromThePositions()
    {
        await _recorder.OnPlaybackAsync(PlaybackState.Playing, 8);
        await PlayAsync(8, 6);
        await _recorder.CloseAsync();

        Last.PlayedSeconds.ShouldBe(6, 0.001);
        var rows = Last.Sections!;
        rows.Single(r => r.SectionId == Intro.Id).PlayedSeconds.ShouldBe(2, 0.26);
        rows.Single(r => r.SectionId == Verse.Id).PlayedSeconds.ShouldBe(4, 0.26);
        rows.Single(r => r.SectionId == Verse.Id).Kind.ShouldBe(PracticeSectionKindDto.Verse);
        rows.Single(r => r.SectionId == Verse.Id).Label.ShouldBe("Verse 1");
        rows.Sum(r => r.PlayedSeconds).ShouldBeLessThanOrEqualTo(Last.PlayedSeconds + 1);
    }

    [Fact]
    public async Task TimeOutsideEverySection_HasNoSectionRow()
    {
        _recorder.Begin(TrackId, null, [Verse]);
        await _recorder.OnPlaybackAsync(PlaybackState.Playing, 0);
        await PlayAsync(0, 3);
        await _recorder.CloseAsync();

        Last.PlayedSeconds.ShouldBe(3, 0.001);
        Last.Sections.ShouldBeNull();
        Last.MixId.ShouldBeNull();
    }

    [Fact]
    public async Task LoopPasses_CountPerSection_AndTheTrainerPercentOnlyForFullSectionPasses()
    {
        await _recorder.OnPlaybackAsync(PlaybackState.Playing, 10);
        await PlayAsync(10, 1);
        _recorder.OnLoopPass(new LoopPassInfo(10, 30, 1, 80), trainerOn: true, loopSectionId: Verse.Id);
        _recorder.OnLoopPass(new LoopPassInfo(10, 30, 2, 85), trainerOn: true, loopSectionId: Verse.Id);
        _recorder.OnLoopPass(new LoopPassInfo(10, 30, 3, 95), trainerOn: false, loopSectionId: Verse.Id);
        _recorder.OnLoopPass(new LoopPassInfo(12, 20, 1, 110), trainerOn: true, loopSectionId: null);
        _recorder.OnLoopPass(new LoopPassInfo(12, 20, 2, 110), trainerOn: true, loopSectionId: Verse.Id);
        await _recorder.CloseAsync();

        var verse = Last.Sections!.Single(r => r.SectionId == Verse.Id);
        verse.LoopPasses.ShouldBe(5);
        verse.TrainerReachedPercent.ShouldBe(85);
        Last.LoopPasses.ShouldBe(5);
    }

    [Fact]
    public async Task LoopPassOutsideEverySection_CountsForTheSessionOnly()
    {
        _recorder.Begin(TrackId, null, [Verse]);
        await _recorder.OnPlaybackAsync(PlaybackState.Playing, 0);
        await PlayAsync(0, 1);
        _recorder.OnLoopPass(new LoopPassInfo(1, 5, 1, 100), trainerOn: true, loopSectionId: Guid.NewGuid());
        await _recorder.CloseAsync();

        Last.LoopPasses.ShouldBe(1);
        Last.Sections.ShouldBeNull();
    }

    [Fact]
    public async Task LoopPasses_AreIgnoredWhileIdle()
    {
        _recorder.OnLoopPass(new LoopPassInfo(10, 30, 1, 80), trainerOn: true, loopSectionId: Verse.Id);
        await _recorder.CloseAsync();

        _writes.ShouldBeEmpty();
    }

    [Fact]
    public async Task MaxSpeed_IsTheHighestPlaybackSpeed_ClampedToTheContractRange()
    {
        await _recorder.OnPlaybackAsync(PlaybackState.Playing, 0);
        await PlayAsync(0, 1, speed: 90);
        await PlayAsync(1, 1, speed: 130);
        await _recorder.CloseAsync();

        Last.MaxSpeedPercent.ShouldBe(120);
    }

    [Fact]
    public async Task FlushesEvery30SecondsWhilePlaying()
    {
        await _recorder.OnPlaybackAsync(PlaybackState.Playing, 0);
        await PlayAsync(0, 29.5, step: 0.5);
        _writes.ShouldBeEmpty();

        await PlayAsync(29.5, 0.5, step: 0.5);
        _writes.Count.ShouldBe(1);
        await PlayAsync(30, 30, step: 0.5);
        _writes.Count.ShouldBe(2);
        Last.PlayedSeconds.ShouldBe(60, 0.001);
    }

    [Fact]
    public async Task TenMinutes_AreRecordedWithin2Seconds()
    {
        await _recorder.OnPlaybackAsync(PlaybackState.Playing, 0);
        await PlayAsync(0, 600, step: 0.37);
        await _recorder.CloseAsync();

        Last.PlayedSeconds.ShouldBe(600, 2);
    }

    [Fact]
    public async Task Conflict_PracticeLogOff_StopsTheRecorder()
    {
        _failure = () => new ApiException(new ApiError(HttpStatusCode.Conflict, "Off", null, new Dictionary<string, string[]>()) { Type = PracticeFlowProblemTypes.BaseUri + PracticeFlowProblemTypes.PracticeLogOff });
        await _recorder.OnPlaybackAsync(PlaybackState.Playing, 0);
        await PlayAsync(0, 2);
        await _recorder.OnPlaybackAsync(PlaybackState.Paused, 2);

        _recorder.State.ShouldBe(PracticeRecorderState.Off);
        _failure = () => null;
        await _recorder.OnPlaybackAsync(PlaybackState.Playing, 2);
        await PlayAsync(2, 40);
        await _recorder.CloseAsync();
        _writes.ShouldBeEmpty();
    }

    [Fact]
    public async Task OtherErrors_KeepTheBuffer_AndRetryOnTheNextFlush()
    {
        _failure = () => new HttpRequestException("down");
        await _recorder.OnPlaybackAsync(PlaybackState.Playing, 0);
        await PlayAsync(0, 3);
        await _recorder.OnPlaybackAsync(PlaybackState.Paused, 3);
        _failure = () => new ApiException(new ApiError(HttpStatusCode.Conflict, "Other", null, new Dictionary<string, string[]>()) { Type = PracticeFlowProblemTypes.BaseUri + "mix-track-mismatch" });
        await _recorder.OnPlaybackAsync(PlaybackState.Playing, 3);
        await _recorder.OnPlaybackAsync(PlaybackState.Paused, 3);
        _writes.ShouldBeEmpty();

        _failure = () => null;
        await _recorder.CloseAsync();

        _writes.Single().Session.PlayedSeconds.ShouldBe(3, 0.001);
        _recorder.State.ShouldBe(PracticeRecorderState.Closed);
    }

    [Fact]
    public async Task AnEmptySession_IsNotWritten()
    {
        await _recorder.OnPlaybackAsync(PlaybackState.Playing, 0);
        await _recorder.OnPlaybackAsync(PlaybackState.Stopped, 0);
        await _recorder.CloseAsync();

        _writes.ShouldBeEmpty();
    }

    [Fact]
    public async Task Begin_ClosesTheRunningSession_AndStartsAFreshContext()
    {
        await _recorder.OnPlaybackAsync(PlaybackState.Playing, 0);
        await PlayAsync(0, 2);
        var other = Guid.NewGuid();

        await _recorder.ChangeTrackAsync(other, null, []);
        _writes.Single().Session.TrackId.ShouldBe(TrackId);
        _recorder.State.ShouldBe(PracticeRecorderState.Idle);

        await _recorder.OnPlaybackAsync(PlaybackState.Playing, 0);
        await PlayAsync(0, 1);
        await _recorder.CloseAsync();
        Last.TrackId.ShouldBe(other);
    }

    [Fact]
    public async Task SectionsAndMix_CanBeUpdatedWhileRunning()
    {
        await _recorder.OnPlaybackAsync(PlaybackState.Playing, 12);
        _recorder.UpdateSections([Verse]);
        _recorder.SetMix(null);
        await PlayAsync(12, 1);
        await _recorder.CloseAsync();

        Last.MixId.ShouldBeNull();
        Last.Sections!.Single().SectionId.ShouldBe(Verse.Id);
    }

    [Fact]
    public async Task PositionsWhileNotRunning_AddNothing()
    {
        await PlayAsync(0, 5);
        await _recorder.OnPlaybackAsync(PlaybackState.Playing, 0);
        await _recorder.OnPlaybackAsync(PlaybackState.Paused, 0);
        await PlayAsync(0, 5);
        await _recorder.OnPlaybackAsync(PlaybackState.Paused, 0);
        await _recorder.CloseAsync();

        _writes.ShouldBeEmpty();
    }

    [Fact]
    public async Task AFlushInFlight_IsNotStartedTwice()
    {
        var gate = new TaskCompletionSource();
        var calls = 0;
        var recorder = new PracticeRecorder(async (_, _, _) => { calls++; await gate.Task; }, _time);
        recorder.Begin(TrackId, null, []);
        await recorder.OnPlaybackAsync(PlaybackState.Playing, 0);
        _time.Advance(TimeSpan.FromSeconds(31));
        var first = recorder.OnPositionAsync(31, 100);
        _time.Advance(TimeSpan.FromSeconds(31));
        await recorder.OnPositionAsync(62, 100);

        calls.ShouldBe(1);
        gate.SetResult();
        await first;
    }

    [Fact]
    public async Task Close_WhileTheHoldFlushIsInFlight_WritesAgainAfterIt_SoALatePassIsKept()
    {
        // E2E practice-log scenario 2: pause, then the last loop pass arrives and the page is left while the pause's write runs.
        var gate = new TaskCompletionSource();
        var bodies = new List<PracticeSessionInputDto>();
        var recorder = new PracticeRecorder(async (_, body, _) => { bodies.Add(body); await gate.Task; }, _time);
        recorder.Begin(TrackId, null, [Intro, Verse]);
        await recorder.OnPlaybackAsync(PlaybackState.Playing, 0);
        _time.Advance(TimeSpan.FromSeconds(10));
        await recorder.OnPositionAsync(10, 90);
        var hold = recorder.OnPlaybackAsync(PlaybackState.Paused, 10);
        recorder.OnLoopPass(new LoopPassInfo(0, 10, 1, 90), trainerOn: true, Intro.Id);

        var close = recorder.CloseAsync();
        bodies.Count.ShouldBe(1);
        gate.SetResult();
        await Task.WhenAll(hold, close);

        bodies.Count.ShouldBe(2);
        bodies[^1].LoopPasses.ShouldBe(1);
        bodies[^1].Sections!.Single(s => s.SectionId == Intro.Id).TrainerReachedPercent.ShouldBe(90);
        recorder.State.ShouldBe(PracticeRecorderState.Closed);
    }

    [Fact]
    public void Arguments_AreChecked()
    {
        Should.Throw<ArgumentNullException>(() => new PracticeRecorder(null!, _time));
        Should.Throw<ArgumentNullException>(() => new PracticeRecorder(WriteAsync, null!));
        Should.Throw<ArgumentNullException>(() => _recorder.Begin(TrackId, null, null!));
        Should.Throw<ArgumentNullException>(() => _recorder.UpdateSections(null!));
        Should.Throw<ArgumentNullException>(() => _recorder.OnLoopPass(null!, false, null));
    }
}
