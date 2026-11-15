namespace StemStudio.Agent.Ports;

/// <summary>
/// UI tool round trip (contracts/DELTA.md section 4): the implementation makes the open page receive the request
/// (ui.requested event) and completes when the page POSTs its result to ui-results, or fails the step after the timeout.
/// </summary>
public interface IUiToolGateway
{
    /// <summary>Sends a UI tool call to the open page and waits for its answer.</summary>
    /// <param name="request">The call.</param>
    /// <param name="cancellationToken">Cancellation (run cancelled).</param>
    /// <returns>The page's answer; <c>Ok = false</c> with "The page did not respond" after the timeout.</returns>
    Task<UiToolResult> RequestAsync(UiToolRequest request, CancellationToken cancellationToken);

    /// <summary>Delivers the page's answer (called by the ui-results endpoint).</summary>
    /// <param name="runId">Run identifier.</param>
    /// <param name="stepId">Step identifier.</param>
    /// <param name="result">The answer.</param>
    /// <returns>False when no answer is expected (already answered or timed out: 409 ui-result-not-expected).</returns>
    bool TryComplete(Guid runId, Guid stepId, UiToolResult result);
}
