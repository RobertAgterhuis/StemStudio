using StemStudio.Agent.Model;
using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.Runtime;

/// <summary>How a run ended: its terminal status, failure and terminal event.</summary>
/// <param name="Status">Terminal status.</param>
/// <param name="EventType">Terminal event type.</param>
/// <param name="Code">Failure code of a failed run.</param>
/// <param name="Message">Readable failure message (never raw exception text).</param>
/// <param name="MessageId">Assistant message of a completed run.</param>
internal sealed record RunOutcome(AgentRunStatusDto Status, string EventType, string? Code, string? Message, Guid? MessageId)
{
    /// <summary>Generic message for unexpected errors; details are only logged.</summary>
    public const string InternalMessage = "The run failed because of an internal error.";

    /// <summary>An unexpected error.</summary>
    public static RunOutcome Internal { get; } = Fail(AgentFailureCodes.Internal, InternalMessage);

    /// <summary>Jobs cancelled together with the run ("phase:id").</summary>
    public IReadOnlyList<string> CancelledJobs { get; init; } = [];

    /// <summary>Cancelled by the user or the host.</summary>
    public static RunOutcome Cancelled(IReadOnlyList<string> jobs) =>
        new(AgentRunStatusDto.Cancelled, AgentEventTypes.RunCancelled, null, null, null) { CancelledJobs = jobs };

    /// <summary>A failure with a code and a readable message (capped at 500 characters).</summary>
    public static RunOutcome Fail(string code, string message) =>
        new(AgentRunStatusDto.Failed, AgentEventTypes.RunFailed, code, message.Length > AgentContractLimits.ErrorMaxLength ? message[..AgentContractLimits.ErrorMaxLength] : message, null);

    /// <summary>A completed run with its answer.</summary>
    public static RunOutcome Completed(Guid messageId) => new(AgentRunStatusDto.Completed, AgentEventTypes.RunCompleted, null, null, messageId);

    /// <summary>The run in its terminal state.</summary>
    public AgentRun Apply(AgentRun run, DateTimeOffset now) => run with { Status = Status, FailureCode = Code, FailureMessage = Message, EndedAt = now };

    /// <summary>The terminal event's data.</summary>
    public object Payload(Guid runId) => Status switch
    {
        AgentRunStatusDto.Completed => new RunCompletedEventDto { RunId = runId, MessageId = MessageId!.Value },
        AgentRunStatusDto.Cancelled => new RunCancelledEventDto { RunId = runId, CancelledJobs = CancelledJobs },
        _ => new RunFailureDto { Code = Code!, Message = Message! },
    };
}
