namespace StemStudio.Web.Services;

/// <summary>
/// Transient confirmations such as "Saved" or "Level set to Expert" (ui-test-ids 1.3.0 <c>toast</c>). One
/// message at a time; it disappears after <see cref="Duration"/>. Scoped per circuit.
/// </summary>
/// <param name="time">Clock for the timeout.</param>
public sealed class ToastService(TimeProvider time) : IDisposable
{
    private CancellationTokenSource? _timeout;

    /// <summary>How long a message stays.</summary>
    public static TimeSpan Duration { get; } = TimeSpan.FromSeconds(4);

    /// <summary>Raised when the message appears or disappears (possibly off the renderer thread).</summary>
    public event Action? Changed;

    /// <summary>The visible message, or <see langword="null"/>.</summary>
    public string? Message { get; private set; }

    /// <summary>Shows a message, replacing the current one.</summary>
    /// <param name="message">Plain text in the voice of design-spec.md 8.</param>
    public void Show(string message)
    {
        Cancel();
        Message = message;
        Changed?.Invoke();
        _timeout = new CancellationTokenSource();
        _ = ClearLaterAsync(_timeout.Token);
    }

    /// <inheritdoc />
    public void Dispose() => Cancel();

    private void Cancel()
    {
        _timeout?.Cancel();
        _timeout?.Dispose();
        _timeout = null;
    }

    private async Task ClearLaterAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(Duration, time, cancellationToken);
            Message = null;
            Changed?.Invoke();
        }
        catch (OperationCanceledException)
        {
            // Replaced by a newer message.
        }
    }
}
