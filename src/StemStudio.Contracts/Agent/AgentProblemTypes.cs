namespace StemStudio.Contracts.Agent;

/// <summary>Problem <c>type</c> codes of the agent endpoints (<c>https://stemstudio.local/problems/&lt;code&gt;</c>).</summary>
public static class AgentProblemTypes
{
    /// <summary>Base URI of every problem type.</summary>
    public const string BaseUri = "https://stemstudio.local/problems/";

    /// <summary>409: Claude selected but no usable key is available (environment variable or, 1.21.0, a readable stored key).</summary>
    public const string ClaudeNotConfigured = "claude-not-configured";

    /// <summary>422: Claude selected before the paid-service notice was acknowledged.</summary>
    public const string NoticeNotAcknowledged = "notice-not-acknowledged";

    /// <summary>422: the Ollama model is not installed or has no tool support.</summary>
    public const string ModelUnavailable = "model-unavailable";

    /// <summary>409: a run is already active (one per conversation and one globally).</summary>
    public const string RunActive = "run-active";

    /// <summary>503: the selected backend is not ready; the problem carries the AgentStatus in the extension member <c>agentStatus</c>.</summary>
    public const string BackendUnavailable = "backend-unavailable";

    /// <summary>409: the approval is no longer pending (expired or the run ended).</summary>
    public const string ApprovalNotPending = "approval-not-pending";

    /// <summary>409: the arguments hash of the decision does not match the step.</summary>
    public const string ArgumentsChanged = "arguments-changed";

    /// <summary>409: the UI result was already submitted or the step timed out.</summary>
    public const string UiResultNotExpected = "ui-result-not-expected";

    /// <summary>409: the conversation has nothing left to undo.</summary>
    public const string NothingToUndo = "nothing-to-undo";

    /// <summary>409: the run already ended (cancel of a terminal run).</summary>
    public const string RunEnded = "run-ended";

    /// <summary>422 (1.21.0): the value is not a well-formed Claude API key; extension <c>reason</c> (<see cref="ClaudeKeyInvalidReasons"/>).</summary>
    public const string ClaudeKeyInvalid = "claude-key-invalid";

    /// <summary>400 (1.21.0): the key body is not a JSON object with <c>apiKey</c>; fixed text, never quotes the body.</summary>
    public const string InvalidBody = "invalid-body";

    /// <summary>403 (1.21.0): key entry or the Claude key test from somewhere other than the computer StemStudio runs on.</summary>
    public const string LocalOnly = "local-only";

    /// <summary>503 (1.21.0): the key ring directory is missing, not writable or misplaced; the environment variable still works.</summary>
    public const string KeyStoreUnavailable = "key-store-unavailable";

    /// <summary>The full <c>type</c> URI of a problem code.</summary>
    /// <param name="code">A code of this class.</param>
    /// <returns>The problem type URI.</returns>
    public static string Uri(string code) => BaseUri + code;
}
