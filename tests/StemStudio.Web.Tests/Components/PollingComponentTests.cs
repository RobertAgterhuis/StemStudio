using StemStudio.Web.Components;

namespace StemStudio.Web.Tests.Components;

/// <summary>The polling base: its stop token stays readable after dispose (late continuations of a removed component).</summary>
public sealed class PollingComponentTests
{
    [Fact]
    public void Stopping_IsLiveBeforeDispose_AndCancelledAfterIt_WithoutThrowing()
    {
        var component = new Probe();
        component.Token.IsCancellationRequested.ShouldBeFalse();

        component.Dispose();

        component.Token.IsCancellationRequested.ShouldBeTrue();
    }

    private sealed class Probe : PollingComponent
    {
        public CancellationToken Token => Stopping;

        protected override bool NeedsPolling => false;

        protected override Task RefreshAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
