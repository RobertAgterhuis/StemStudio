using Microsoft.AspNetCore.Components;
using StemStudio.Web.Services;

namespace StemStudio.Web.Components;

/// <summary>
/// Base of components that poll the API about once per second while something is in progress (track page, band info
/// panel, library): one <see cref="PollLoop"/>, re-render after every poll, cancelled when the component is disposed.
/// </summary>
public abstract class PollingComponent : ComponentBase, IDisposable
{
    private readonly CancellationTokenSource _stop = new();
    private readonly CancellationToken _stopping;
    private PollLoop? _loop;

    /// <summary>Creates the component; the stop token is taken once so it stays readable after dispose.</summary>
    protected PollingComponent() => _stopping = _stop.Token;

    /// <summary>The polling timer.</summary>
    [Inject]
    protected IPollingTimer Timer { get; set; } = default!;

    /// <summary>
    /// Cancelled when the component is disposed; pass it to API calls. Readable after dispose (a continuation of a
    /// component that was removed while it awaited, such as a folder view replaced by search results, gets a cancelled
    /// token instead of an <see cref="ObjectDisposedException"/> that would end the circuit).
    /// </summary>
    protected CancellationToken Stopping => _stopping;

    /// <summary>Whether another poll is needed.</summary>
    protected abstract bool NeedsPolling { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>One poll: reload the state (errors are shown, not thrown).</summary>
    /// <param name="ct">Cancelled on dispose.</param>
    /// <returns>A task that completes when the state is reloaded.</returns>
    protected abstract Task RefreshAsync(CancellationToken ct);

    /// <summary>Called once when polling stopped because nothing is in progress any more.</summary>
    /// <returns>A task.</returns>
    protected virtual Task OnSettledAsync() => Task.CompletedTask;

    /// <summary>Starts polling unless it is running or not needed.</summary>
    protected void StartPolling() => (_loop ??= new PollLoop(Timer)).Start(() => NeedsPolling, PollOnceAsync, OnSettledAsync, _stopping);

    /// <summary>Stops polling and releases the cancellation source.</summary>
    /// <param name="disposing">True when called from <see cref="Dispose()"/>.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _stop.Cancel();
            _stop.Dispose();
        }
    }

    private async Task PollOnceAsync(CancellationToken ct)
    {
        await RefreshAsync(ct);
        await InvokeAsync(StateHasChanged);
    }
}
