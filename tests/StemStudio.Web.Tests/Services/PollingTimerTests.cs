using Microsoft.Extensions.Time.Testing;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Services;

namespace StemStudio.Web.Tests.Services;

public sealed class PollingTimerTests
{
    [Fact]
    public async Task Wait_CompletesAfterOneSecond_OnTheTimeProvider()
    {
        var time = new FakeTimeProvider();
        var timer = new PollingTimer(time);

        var wait = timer.WaitAsync(TestContext.Current.CancellationToken);
        time.Advance(TimeSpan.FromMilliseconds(999));
        wait.IsCompleted.ShouldBeFalse();
        time.Advance(TimeSpan.FromMilliseconds(1));

        (await wait).ShouldBeTrue();
        PollingTimer.Interval.ShouldBe(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Wait_ReturnsFalse_WhenCancelled()
    {
        var timer = new PollingTimer(new FakeTimeProvider());
        using var cts = new CancellationTokenSource();

        var wait = timer.WaitAsync(cts.Token);
        await cts.CancelAsync();

        (await wait).ShouldBeFalse();
    }

    [Theory]
    [InlineData(TrackStatusDto.Queued, false, "Queued")]
    [InlineData(TrackStatusDto.Downloading, false, "Downloading")]
    [InlineData(TrackStatusDto.Separating, false, "Separating")]
    [InlineData(TrackStatusDto.Analysing, false, "Analysing")]
    [InlineData(TrackStatusDto.Completed, true, "Completed")]
    [InlineData(TrackStatusDto.Failed, true, "Failed")]
    public void TrackStatusText_LabelsAndTerminalStates(TrackStatusDto status, bool terminal, string label)
    {
        TrackStatusText.IsTerminal(status).ShouldBe(terminal);
        TrackStatusText.Label(status).ShouldBe(label);
    }

    [Theory]
    [InlineData(null, "-")]
    [InlineData(0.0, "0:00")]
    [InlineData(59.9, "0:59")]
    [InlineData(213.04, "3:33")]
    [InlineData(900.0, "15:00")]
    public void TimeText_Duration(double? seconds, string expected)
    {
        TimeText.Duration(seconds).ShouldBe(expected);
    }

    [Theory]
    [InlineData(0.0, "0:00.0")]
    [InlineData(12.34, "0:12.3")]
    [InlineData(75.06, "1:15.0")]
    [InlineData(-1.0, "0:00.0")]
    public void TimeText_Position(double seconds, string expected)
    {
        TimeText.Position(seconds).ShouldBe(expected);
    }
}
