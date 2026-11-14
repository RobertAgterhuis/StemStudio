using StemStudio.Web.Services;

namespace StemStudio.Web.Tests.Fakes;

/// <summary>Deterministic polling timer: each <see cref="Tick"/> releases one pending wait.</summary>
public sealed class ManualPollingTimer : IPollingTimer
{
    private TaskCompletionSource<bool> _pending = NewSource();

    public int Waits { get; private set; }

    public Task<bool> WaitAsync(CancellationToken cancellationToken)
    {
        Waits++;
        var source = _pending;
        cancellationToken.Register(() => source.TrySetResult(false));
        return source.Task;
    }

    public void Tick()
    {
        var source = _pending;
        _pending = NewSource();
        source.TrySetResult(true);
    }

    private static TaskCompletionSource<bool> NewSource() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
