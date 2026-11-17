namespace StemStudio.Persistence.Agent;

/// <summary>
/// The single agent settings row (feature 011, data-model 2). Enum values are stored by their C# name; the API maps the row to
/// the agent's <c>AgentSettings</c> record. Never holds an API key.
/// </summary>
public sealed class AgentSettingsEntity
{
    /// <summary>The fixed id of the only row.</summary>
    public const int SingletonId = 1;

    /// <summary>Row id, always <see cref="SingletonId"/>.</summary>
    public int Id { get; set; } = SingletonId;

    /// <summary>Selected backend (ollama, claude, none).</summary>
    public string Backend { get; set; } = string.Empty;

    /// <summary>Default Ollama model.</summary>
    public string OllamaModel { get; set; } = string.Empty;

    /// <summary>CPU fallback Ollama model.</summary>
    public string OllamaFallbackModel { get; set; } = string.Empty;

    /// <summary>Ollama thinking mode.</summary>
    public bool OllamaThink { get; set; }

    /// <summary>Claude model.</summary>
    public string ClaudeModel { get; set; } = string.Empty;

    /// <summary>Claude effort.</summary>
    public string ClaudeEffort { get; set; } = string.Empty;

    /// <summary>When the user acknowledged the Claude notice (UTC), if ever.</summary>
    public DateTime? ClaudeNoticeAcknowledgedAt { get; set; }

    /// <summary>Run limits as JSON.</summary>
    public string Limits { get; set; } = "{}";

    /// <summary>Last change (UTC).</summary>
    public DateTime UpdatedAt { get; set; }
}

/// <summary>An agent conversation (feature 011, data-model 3).</summary>
public sealed class ConversationEntity
{
    /// <summary>Conversation id.</summary>
    public Guid Id { get; set; }

    /// <summary>Title, at most 120 characters.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Creation time (UTC).</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Last activity (UTC).</summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>The run that is not terminal yet, if any.</summary>
    public Guid? ActiveRunId { get; set; }
}

/// <summary>A user or assistant message of a conversation (feature 011, data-model 3).</summary>
public sealed class ConversationMessageEntity
{
    /// <summary>Message id.</summary>
    public Guid Id { get; set; }

    /// <summary>Owning conversation.</summary>
    public Guid ConversationId { get; set; }

    /// <summary>Monotonic position within the conversation (unique per conversation).</summary>
    public int Seq { get; set; }

    /// <summary>user or assistant.</summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>Message text.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Page of the context.</summary>
    public string Page { get; set; } = string.Empty;

    /// <summary>Track of the context, if any.</summary>
    public Guid? TrackId { get; set; }

    /// <summary>Mix of the context, if any.</summary>
    public Guid? MixId { get; set; }

    /// <summary>Lyrics sheet of the context, if any (feature 023).</summary>
    public Guid? LyricsId { get; set; }

    /// <summary>The run the message started or answered, if any.</summary>
    public Guid? RunId { get; set; }

    /// <summary>Creation time (UTC).</summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>One agent run for a user command (feature 011, data-model 4.1).</summary>
public sealed class AgentRunEntity
{
    /// <summary>Run id.</summary>
    public Guid Id { get; set; }

    /// <summary>Owning conversation.</summary>
    public Guid ConversationId { get; set; }

    /// <summary>The user message (command) of the run.</summary>
    public Guid UserMessageId { get; set; }

    /// <summary>Run status.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Backend of the run.</summary>
    public string Backend { get; set; } = string.Empty;

    /// <summary>Model of the first turn.</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>True after untrusted text entered the run.</summary>
    public bool Tainted { get; set; }

    /// <summary>Plan items as JSON.</summary>
    public string Plan { get; set; } = "[]";

    /// <summary>Model turns so far.</summary>
    public int ModelTurns { get; set; }

    /// <summary>Tool calls so far.</summary>
    public int ToolCalls { get; set; }

    /// <summary>Invalid tool calls so far.</summary>
    public int InvalidCalls { get; set; }

    /// <summary>Input tokens.</summary>
    public long InputTokens { get; set; }

    /// <summary>Output tokens.</summary>
    public long OutputTokens { get; set; }

    /// <summary>Cached input tokens.</summary>
    public long CachedInputTokens { get; set; }

    /// <summary>Failure code, if failed.</summary>
    public string? FailureCode { get; set; }

    /// <summary>Readable failure message, if failed.</summary>
    public string? FailureMessage { get; set; }

    /// <summary>Start time (UTC).</summary>
    public DateTime StartedAt { get; set; }

    /// <summary>End time (UTC), when terminal.</summary>
    public DateTime? EndedAt { get; set; }
}

/// <summary>One tool call of a run (feature 011, data-model 4.2).</summary>
public sealed class RunStepEntity
{
    /// <summary>Step id (also the approval and UI round-trip id).</summary>
    public Guid Id { get; set; }

    /// <summary>Owning run.</summary>
    public Guid RunId { get; set; }

    /// <summary>Position within the run.</summary>
    public int Seq { get; set; }

    /// <summary>The model's call id.</summary>
    public string CallId { get; set; } = string.Empty;

    /// <summary>Tool name.</summary>
    public string Tool { get; set; } = string.Empty;

    /// <summary>Tool kind.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Risk class.</summary>
    public string Risk { get; set; } = string.Empty;

    /// <summary>Undo kind.</summary>
    public string UndoKind { get; set; } = string.Empty;

    /// <summary>Canonical JSON arguments.</summary>
    public string Arguments { get; set; } = "{}";

    /// <summary>SHA-256 hex of the canonical arguments.</summary>
    public string ArgumentsHash { get; set; } = string.Empty;

    /// <summary>Linked plan item, if any.</summary>
    public int? PlanIndex { get; set; }

    /// <summary>Policy decision.</summary>
    public string Decision { get; set; } = string.Empty;

    /// <summary>Decision reason codes as a JSON array.</summary>
    public string DecisionReasons { get; set; } = "[]";

    /// <summary>Step status.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Start time (UTC).</summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>End time (UTC).</summary>
    public DateTime? EndedAt { get; set; }

    /// <summary>Estimated minutes for heavy steps.</summary>
    public double? EstimatedMinutes { get; set; }

    /// <summary>Projected result as JSON.</summary>
    public string? ResultSummary { get; set; }

    /// <summary>Readable error.</summary>
    public string? Error { get; set; }

    /// <summary>Job progress 0-100.</summary>
    public int? Progress { get; set; }

    /// <summary>Track of the job, if any.</summary>
    public Guid? JobTrackId { get; set; }

    /// <summary>Job id, if any.</summary>
    public string? JobId { get; set; }

    /// <summary>Job phase, when the step refers to a job.</summary>
    public string? JobPhase { get; set; }
}

/// <summary>The approval of a step (feature 011, data-model 4.3); the id equals the step id.</summary>
public sealed class ApprovalEntity
{
    /// <summary>Step id.</summary>
    public Guid Id { get; set; }

    /// <summary>Owning run.</summary>
    public Guid RunId { get; set; }

    /// <summary>Approval status.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Approval scope.</summary>
    public string Scope { get; set; } = string.Empty;

    /// <summary>Copy of the step's arguments hash.</summary>
    public string ArgumentsHash { get; set; } = string.Empty;

    /// <summary>Request time (UTC).</summary>
    public DateTime RequestedAt { get; set; }

    /// <summary>Decision time (UTC).</summary>
    public DateTime? DecidedAt { get; set; }
}

/// <summary>A recorded inverse or compensating action (feature 011, data-model 4.4).</summary>
public sealed class UndoEntryEntity
{
    /// <summary>Entry id.</summary>
    public Guid Id { get; set; }

    /// <summary>Owning conversation.</summary>
    public Guid ConversationId { get; set; }

    /// <summary>Run of the reversed step (the undo group).</summary>
    public Guid RunId { get; set; }

    /// <summary>The step the entry reverses.</summary>
    public Guid StepId { get; set; }

    /// <summary>Insertion order within the conversation (newest = highest).</summary>
    public int Ordinal { get; set; }

    /// <summary>Inverse or Compensate.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Tool of the undo action.</summary>
    public string ActionTool { get; set; } = string.Empty;

    /// <summary>Canonical JSON arguments of the undo action.</summary>
    public string ActionArguments { get; set; } = "{}";

    /// <summary>True when the compensated artefact is fresh (no approval needed).</summary>
    public bool Fresh { get; set; }

    /// <summary>The target mix's <c>updatedAt</c> (UTC) right after the step; null for unversioned targets (data-model 4.4).</summary>
    public DateTime? ExpectedUpdatedAt { get; set; }

    /// <summary>Available, Applied or Obsolete.</summary>
    public string Status { get; set; } = string.Empty;
}

/// <summary>An append-only run event (feature 011, data-model 4.5); key (RunId, Seq), Seq from 1.</summary>
public sealed class RunEventEntity
{
    /// <summary>Owning run.</summary>
    public Guid RunId { get; set; }

    /// <summary>Position in the run's event log, from 1 (the SSE id).</summary>
    public int Seq { get; set; }

    /// <summary>Event type, e.g. <c>run.started</c>.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>JSON data.</summary>
    public string Payload { get; set; } = "{}";

    /// <summary>Creation time (UTC).</summary>
    public DateTime CreatedAt { get; set; }
}
