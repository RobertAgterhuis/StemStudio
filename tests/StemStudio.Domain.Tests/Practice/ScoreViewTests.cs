using StemStudio.Domain.Practice;

namespace StemStudio.Domain.Tests.Practice;

/// <summary>The score view entity (014 FR-014, data-model 3.5).</summary>
public sealed class ScoreViewTests
{
    private const string JobId = "89abcdef0123456789abcdef01234567";
    private static readonly DateTime T0 = new(2026, 11, 16, 10, 12, 0, DateTimeKind.Utc);

    private static ScoreBar Bar(int index, double start, double end) =>
        new() { Index = index, Number = index + 1, Page = 1, X = 0.1, Y = 0.1, Width = 0.2, Height = 0.03, StartSeconds = start, EndSeconds = end };

    [Fact]
    public void Lifecycle_QueuedRenderingReady_ShiftsBarsByTheOffset()
    {
        var trackId = Guid.NewGuid();
        var view = ScoreView.Create(Guid.NewGuid(), trackId, 6.1, T0);
        (view.Status, view.IsActive, view.TrackId, view.Revision).ShouldBe((ScoreViewStatus.Queued, true, (Guid?)trackId, 0));

        view.Submit(JobId, T0);
        view.MarkRendering(T0.AddSeconds(1));
        view.Status.ShouldBe(ScoreViewStatus.Rendering);
        view.Complete(2, [Bar(0, 0, 2.1), Bar(1, 2.1, 4.1)], T0.AddSeconds(2));

        (view.Status, view.PageCount, view.Revision, view.WorkerJobId, view.IsActive).ShouldBe((ScoreViewStatus.Ready, 2, 1, (string?)null, false));
        view.Bars.Select(bar => bar.StartSeconds).ShouldBe([6.1, 8.2], 1e-9);
        view.Bars.Select(bar => bar.EndSeconds).ShouldBe([8.2, 10.2], 1e-9);
    }

    [Fact]
    public void MarkRendering_OnlyFromQueued()
    {
        var view = ScoreView.Create(Guid.NewGuid(), null, 0, T0);
        view.Fail(null, T0);

        view.MarkRendering(T0);

        (view.Status, view.Error).ShouldBe((ScoreViewStatus.Failed, ScoreView.DefaultFailure));
    }

    [Fact]
    public void Fail_TrimsTheText_AndSubmitQueuesAgain()
    {
        var view = ScoreView.Create(Guid.NewGuid(), null, 0, T0);
        view.Fail(new string('x', 600), T0);
        view.Error!.Length.ShouldBe(500);

        view.Submit(JobId, T0.AddMinutes(1));

        (view.Status, view.Error, view.WorkerJobId).ShouldBe((ScoreViewStatus.Queued, (string?)null, JobId));
    }

    [Fact]
    public void BadInput_IsRefused()
    {
        var view = ScoreView.Create(Guid.NewGuid(), null, 0, T0);

        Should.Throw<ArgumentOutOfRangeException>(() => ScoreView.Create(Guid.NewGuid(), null, -1, T0));
        Should.Throw<ArgumentException>(() => view.Submit("nope", T0));
        Should.Throw<ArgumentOutOfRangeException>(() => view.Complete(0, [], T0));
        Should.Throw<ArgumentOutOfRangeException>(() => view.Complete(201, [], T0));
        Should.Throw<ArgumentOutOfRangeException>(() => view.Complete(1, Enumerable.Range(0, 2001).Select(i => Bar(i, i, i + 1)).ToList(), T0));
    }
}
