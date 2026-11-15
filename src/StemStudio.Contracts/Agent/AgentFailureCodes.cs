namespace StemStudio.Contracts.Agent;

/// <summary>Values of <see cref="RunFailureDto.Code"/> (data-model 4.1).</summary>
public static class AgentFailureCodes
{
    /// <summary>The backend became unreachable or rejected the request.</summary>
    public const string BackendUnavailable = "backend-unavailable";

    /// <summary>The model-turn or tool-call limit was reached.</summary>
    public const string LimitReached = "limit-reached";

    /// <summary>Too many unknown tools or invalid arguments.</summary>
    public const string InvalidCalls = "invalid-calls";

    /// <summary>A model turn or job wait timed out.</summary>
    public const string Timeout = "timeout";

    /// <summary>The model refused the request (Claude <c>stop_reason: refusal</c>).</summary>
    public const string Refusal = "refusal";

    /// <summary>The per-run Claude token budget is used up.</summary>
    public const string BudgetExhausted = "budget-exhausted";

    /// <summary>Unexpected error (details only in the log, never secrets).</summary>
    public const string Internal = "internal";
}
