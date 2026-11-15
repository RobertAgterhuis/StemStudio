namespace StemStudio.Contracts.Agent;

/// <summary>Per-run limits (data-model 2); ranges are enforced by PUT /api/agent/settings (422).</summary>
public sealed record AgentLimitsDto
{
    /// <summary>Model turns per run, 1-30 (default 12).</summary>
    public required int MaxModelTurns { get; init; }

    /// <summary>Tool calls per run, 1-50 (default 20).</summary>
    public required int MaxToolCalls { get; init; }

    /// <summary>Unknown or invalid calls before the run stops, 1-10 (default 3).</summary>
    public required int MaxInvalidCalls { get; init; }

    /// <summary>Timeout of one local model turn, 10-600 s (default 120).</summary>
    public required int TurnTimeoutSeconds { get; init; }

    /// <summary>Timeout of one Claude turn, 10-600 s (default 180).</summary>
    public required int ClaudeTurnTimeoutSeconds { get; init; }

    /// <summary>Claude input tokens per run, 1,000-1,000,000 (default 60,000).</summary>
    public required int ClaudeMaxInputTokensPerRun { get; init; }

    /// <summary>Claude output tokens per run, 500-128,000 (default 8,000).</summary>
    public required int ClaudeMaxOutputTokensPerRun { get; init; }

    /// <summary>Longest wait_for_job, 1-180 minutes (default 45).</summary>
    public required int MaxJobWaitMinutes { get; init; }

    /// <summary>The defaults of data-model 2.</summary>
    public static AgentLimitsDto Default { get; } = new()
    {
        MaxModelTurns = 12,
        MaxToolCalls = 20,
        MaxInvalidCalls = 3,
        TurnTimeoutSeconds = 120,
        ClaudeTurnTimeoutSeconds = 180,
        ClaudeMaxInputTokensPerRun = 60_000,
        ClaudeMaxOutputTokensPerRun = 8_000,
        MaxJobWaitMinutes = 45,
    };
}
