using System.Collections.Concurrent;
using StemStudio.Agent.Ports;

namespace StemStudio.Agent.Runtime;

/// <summary>
/// The in-memory UI tool round trip (contracts/DELTA.md section 4, one instance per process): <see cref="RequestAsync"/>
/// registers the wait before it returns its task, so an answer that arrives right after <c>ui.requested</c> is not lost; the
/// ui-results endpoint completes it through <see cref="TryComplete"/> once; no answer within the timeout fails the step.
/// </summary>
/// <param name="time">Clock (timeout).</param>
public sealed class UiToolGateway(TimeProvider time) : IUiToolGateway
{
    private readonly ConcurrentDictionary<(Guid RunId, Guid StepId), TaskCompletionSource<UiToolResult>> _pending = new();

    /// <inheritdoc />
    public Task<UiToolResult> RequestAsync(UiToolRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var key = (request.RunId, request.StepId);
        var answer = new TaskCompletionSource<UiToolResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[key] = answer;
        return WaitAsync(key, answer, request.Timeout, cancellationToken);
    }

    /// <inheritdoc />
    public bool TryComplete(Guid runId, Guid stepId, UiToolResult result) =>
        _pending.TryRemove((runId, stepId), out var answer) && answer.TrySetResult(result);

    private async Task<UiToolResult> WaitAsync((Guid, Guid) key, TaskCompletionSource<UiToolResult> answer, TimeSpan timeout, CancellationToken cancellationToken)
    {
        try
        {
            return await answer.Task.WaitAsync(timeout, time, cancellationToken);
        }
        catch (TimeoutException)
        {
            return new UiToolResult { Ok = false, Error = $"The page did not respond within {timeout.TotalSeconds:0} seconds." };
        }
        finally
        {
            _pending.TryRemove(new KeyValuePair<(Guid, Guid), TaskCompletionSource<UiToolResult>>(key, answer));
        }
    }
}
