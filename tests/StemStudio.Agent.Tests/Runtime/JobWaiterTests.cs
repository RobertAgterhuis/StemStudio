using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using StemStudio.Agent.Model;
using StemStudio.Agent.Ports;
using StemStudio.Agent.Runtime;
using StemStudio.Agent.Tests.Fakes;
using StemStudio.Agent.Tests.Fixtures;
using StemStudio.Agent.Tools.Providers;
using StemStudio.Contracts.Agent;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Generation;
using StemStudio.Contracts.Lyrics;
using StemStudio.Contracts.Tracks;
using static StemStudio.Agent.Tests.Fakes.TestTools;

namespace StemStudio.Agent.Tests.Runtime;

/// <summary>T026: wait_for_job polls without model turns (FR-009).</summary>
public sealed class JobWaiterTests
{
    private static readonly Guid TrackId = Guid.NewGuid();
    private readonly IStemStudioActions _actions = Substitute.For<IStemStudioActions>();
    private readonly FakeTimeProvider _time = new(RunnerHarness.Start);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static JobRef Standard => new() { Phase = JobPhaseDto.Standard, TrackId = TrackId };

    private async Task<T> AdvanceUntilAsync<T>(Task<T> task, TimeSpan step)
    {
        for (var i = 0; i < 200 && !task.IsCompleted; i++)
        {
            await Task.Delay(5, Token);
            _time.Advance(step);
        }

        return await task.WaitAsync(TimeSpan.FromSeconds(10), Token);
    }

    [Fact]
    public async Task Track_IsPolledEveryTwoSecondsUntilCompleted_ReportingProgress()
    {
        _actions.Tracks.GetTrackAsync(TrackId, Arg.Any<CancellationToken>()).Returns(
            Dtos.Track(TrackId, status: TrackStatusDto.Separating, progress: 10),
            Dtos.Track(TrackId, status: TrackStatusDto.Separating, progress: 50),
            Dtos.Track(TrackId, status: TrackStatusDto.Completed, progress: 100));
        var progress = new List<int>();
        var waiter = new JobWaiter(_actions, _time);

        var wait = waiter.WaitAsync(new JobWait(Standard, TimeSpan.FromMinutes(45)) { OnProgress = (poll, _) => Report(progress, poll) }, Token);
        await Task.Delay(50, Token);
        await _actions.Tracks.Received(1).GetTrackAsync(TrackId, Arg.Any<CancellationToken>());
        var result = await AdvanceUntilAsync(wait, TimeSpan.FromSeconds(2));

        (result.Status, result.Progress, result.IsTerminal).ShouldBe((JobPoll.Completed, 100, true));
        progress.ShouldBe([10, 50]);
        await _actions.Tracks.Received(3).GetTrackAsync(TrackId, Arg.Any<CancellationToken>());
        (_time.GetUtcNow() - RunnerHarness.Start).ShouldBeGreaterThanOrEqualTo(TimeSpan.FromSeconds(4));
    }

    [Fact]
    public async Task FailedJob_EndsWithItsError()
    {
        _actions.Tracks.GetTrackAsync(TrackId, Arg.Any<CancellationToken>()).Returns(Dtos.Track(TrackId, status: TrackStatusDto.Failed, progress: 30) with { Error = "Download failed" });

        var result = await new JobWaiter(_actions, _time).WaitAsync(new JobWait(Standard, TimeSpan.FromMinutes(1)), Token);

        (result.Status, result.Error, result.Untrusted).ShouldBe((JobPoll.Failed, JobPoll.FailedMessage, "Download failed"));
    }

    [Fact]
    public async Task MaxWait_EndsWithTimeout()
    {
        _actions.Tracks.GetTrackAsync(TrackId, Arg.Any<CancellationToken>()).Returns(Dtos.Track(TrackId, status: TrackStatusDto.Separating, progress: 5));
        var wait = new JobWaiter(_actions, _time).WaitAsync(new JobWait(Standard, TimeSpan.FromMinutes(1)), Token);

        var result = await AdvanceUntilAsync(wait, TimeSpan.FromSeconds(61));

        result.Status.ShouldBe(JobPoll.Timeout);
        result.Error!.ShouldContain("1 minute");
    }

    [Fact]
    public async Task Cancellation_StopsTheWait()
    {
        _actions.Tracks.GetTrackAsync(TrackId, Arg.Any<CancellationToken>()).Returns(Dtos.Track(TrackId, status: TrackStatusDto.Queued, progress: 0));
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var wait = new JobWaiter(_actions, _time).WaitAsync(new JobWait(Standard, TimeSpan.FromMinutes(1)), cancel.Token);

        await cancel.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => wait);
    }

    public static TheoryData<DetailPhaseStatusDto, string> DetailStates => new()
    {
        { DetailPhaseStatusDto.Queued, JobPoll.Running },
        { DetailPhaseStatusDto.Running, JobPoll.Running },
        { DetailPhaseStatusDto.Completed, JobPoll.Completed },
        { DetailPhaseStatusDto.Failed, JobPoll.Failed },
        { DetailPhaseStatusDto.Cancelled, JobPoll.Cancelled },
        { DetailPhaseStatusDto.None, JobPoll.Failed },
    };

    [Theory]
    [MemberData(nameof(DetailStates))]
    public async Task Detail_MapsThePhaseStatus(DetailPhaseStatusDto status, string expected)
    {
        _actions.Tracks.GetTrackAsync(TrackId, Arg.Any<CancellationToken>()).Returns(Dtos.Track(TrackId).WithDetail(status, 40));

        var poll = await JobStatusReader.ReadAsync(_actions, new JobRef { Phase = JobPhaseDto.Detail, TrackId = TrackId }, Token);

        poll.Status.ShouldBe(expected);
    }

    [Theory]
    [InlineData(LyricsStatusDto.Searching, JobPoll.Running)]
    [InlineData(LyricsStatusDto.Aligning, JobPoll.Running)]
    [InlineData(LyricsStatusDto.AlignPending, JobPoll.Running)]
    [InlineData(LyricsStatusDto.Found, JobPoll.Completed)]
    [InlineData(LyricsStatusDto.NotFound, JobPoll.Completed)]
    [InlineData(LyricsStatusDto.Failed, JobPoll.Failed)]
    public async Task Lyrics_MapsTheLyricsStatus(LyricsStatusDto status, string expected)
    {
        _actions.Tracks.GetTrackAsync(TrackId, Arg.Any<CancellationToken>()).Returns(Dtos.Track(TrackId) with { LyricsStatus = status });

        var poll = await JobStatusReader.ReadAsync(_actions, new JobRef { Phase = JobPhaseDto.Lyrics, TrackId = TrackId }, Token);

        poll.Status.ShouldBe(expected);
    }

    [Theory]
    [InlineData(AssetStatusDto.Queued, JobPoll.Running)]
    [InlineData(AssetStatusDto.Rendering, JobPoll.Running)]
    [InlineData(AssetStatusDto.Ready, JobPoll.Completed)]
    [InlineData(AssetStatusDto.Failed, JobPoll.Failed)]
    public async Task Assets_MapTheAssetStatus(AssetStatusDto status, string expected)
    {
        var id = Guid.NewGuid();
        _actions.Assets.GetAssetAsync(id, Arg.Any<CancellationToken>()).Returns(Dtos.Asset(id, status, 70, "Disk full"));

        var export = await JobStatusReader.ReadAsync(_actions, new JobRef { Phase = JobPhaseDto.Export, JobId = id.ToString() }, Token);
        var score = await JobStatusReader.ReadAsync(_actions, new JobRef { Phase = JobPhaseDto.Transcription, JobId = id.ToString() }, Token);

        (export.Status, score.Status, export.Progress).ShouldBe((expected, expected, 70));
    }

    [Theory]
    [InlineData(GenerationStateDto.WaitingForGpu, JobPoll.Running)]
    [InlineData(GenerationStateDto.DownloadingModel, JobPoll.Running)]
    [InlineData(GenerationStateDto.Completed, JobPoll.Completed)]
    [InlineData(GenerationStateDto.Failed, JobPoll.Failed)]
    [InlineData(GenerationStateDto.Cancelled, JobPoll.Cancelled)]
    public async Task Generations_MapTheGenerationState(GenerationStateDto status, string expected)
    {
        var id = Guid.NewGuid();
        _actions.Generations.GetGenerationAsync(id, Arg.Any<CancellationToken>()).Returns(Dtos.Generation(id, status, 20));

        var restyle = await JobStatusReader.ReadAsync(_actions, new JobRef { Phase = JobPhaseDto.Restyle, JobId = id.ToString() }, Token);
        var song = await JobStatusReader.ReadAsync(_actions, new JobRef { Phase = JobPhaseDto.Generation, JobId = id.ToString() }, Token);

        (restyle.Status, song.Status).ShouldBe((expected, expected));
    }

    [Fact]
    public async Task MissingIds_AreReadableFailures()
    {
        var poll = await JobStatusReader.ReadAsync(_actions, new JobRef { Phase = JobPhaseDto.Export, JobId = "not-a-guid" }, Token);
        var track = await JobStatusReader.ReadAsync(_actions, new JobRef { Phase = JobPhaseDto.Standard }, Token);

        (poll.Status, track.Status).ShouldBe((JobPoll.Failed, JobPoll.Failed));
        poll.Error!.ShouldContain("jobId");
        track.Error!.ShouldContain("trackId");
    }

    [Fact]
    public async Task WaitForJob_InARun_PollsWithoutModelTurns()
    {
        var harness = RunnerHarness.Scripted([.. new MetaToolProvider().GetTools()], Turn(Call("wait_for_job", $$"""{"phase":"standard","trackId":"{{TrackId}}"}""")), Say("Ready."));
        harness.Actions.Tracks.GetTrackAsync(TrackId, Arg.Any<CancellationToken>()).Returns(
            Dtos.Track(TrackId, status: TrackStatusDto.Separating, progress: 30),
            Dtos.Track(TrackId, status: TrackStatusDto.Completed, progress: 100));
        await harness.SeedAsync();

        var execution = harness.Runner().ExecuteAsync(harness.Run.Id, Token);
        await harness.Events.WaitForAsync(AgentEventTypes.StepUpdated);
        harness.Script.Requests.Count.ShouldBe(1);
        for (var i = 0; i < 200 && !execution.IsCompleted; i++)
        {
            await Task.Delay(5, Token);
            harness.Time.Advance(TimeSpan.FromSeconds(2));
        }

        await execution.WaitAsync(TimeSpan.FromSeconds(10), Token);

        var step = harness.Runs.Steps.Single();
        (step.Status, step.Progress, step.Job!.Phase).ShouldBe((AgentStepStatusDto.Succeeded, 100, JobPhaseDto.Standard));
        step.ResultSummary.ShouldBe("""{"status":"completed","progress":100}""");
        harness.Events.Payload(AgentEventTypes.StepUpdated).GetProperty("status").GetString().ShouldBe("waiting_job");
        harness.Events.Payload(AgentEventTypes.StepUpdated).GetProperty("progress").GetInt32().ShouldBe(30);
        harness.Script.Requests.Count.ShouldBe(2);
    }

    private static Task Report(List<int> progress, JobPoll poll)
    {
        progress.Add(poll.Progress);
        return Task.CompletedTask;
    }
}
