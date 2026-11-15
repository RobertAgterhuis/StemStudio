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
    private PollLoop? _loop;

    /// <summary>The polling timer.</summary>
    [Inject]
    protected IPollingTimer Timer { get; set; } = default!;

    /// <summary>Cancelled when the component is disposed; pass it to API calls.</summary>
    protected CancellationToken Stopping => _stop.Token;

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
    protected void StartPolling() => (_loop ??= new PollLoop(Timer)).Start(() => NeedsPolling, PollOnceAsync, OnSettledAsync, _stop.Token);

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
