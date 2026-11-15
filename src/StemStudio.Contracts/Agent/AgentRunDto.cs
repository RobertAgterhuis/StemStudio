namespace StemStudio.Contracts.Agent;

/// <summary>Snapshot of a run (GET /api/agent/runs/{runId}).</summary>
public sealed record AgentRunDto
{
    /// <summary>Run identifier.</summary>
    public required Guid RunId { get; init; }

    /// <summary>Conversation of the run.</summary>
    public required Guid ConversationId { get; init; }

    /// <summary>Run status.</summary>
    public required AgentRunStatusDto Status { get; init; }

    /// <summary>Backend of the run.</summary>
    public required AgentBackendDto Backend { get; init; }

    /// <summary>Model of the first turn (later model changes are events).</summary>
    public required string Model { get; init; }

    /// <summary>True once external text entered the run (never reset).</summary>
    public required bool Tainted { get; init; }

    /// <summary>Plan checklist (empty without a plan).</summary>
    public IReadOnlyList<PlanItemDto> Plan { get; init; } = [];

    /// <summary>Steps in order.</summary>
    public IReadOnlyList<AgentStepDto> Steps { get; init; } = [];

    /// <summary>The open confirmation card, when awaiting approval.</summary>
    public ApprovalRequestDto? PendingApproval { get; init; }

    /// <summary>Token usage.</summary>
    public required TokenUsageDto Usage { get; init; }

    /// <summary>Failure of a failed run.</summary>
    public RunFailureDto? Failure { get; init; }

    /// <summary>Start (UTC).</summary>
    public required DateTimeOffset StartedAt { get; init; }

    /// <summary>End (UTC) of a terminal run.</summary>
    public DateTimeOffset? EndedAt { get; init; }
}
