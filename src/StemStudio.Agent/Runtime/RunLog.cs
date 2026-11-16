using Microsoft.Extensions.Logging;

namespace StemStudio.Agent.Runtime;

/// <summary>Server-side log messages of the runner (details stay in the log, never in run records or events).</summary>
internal static partial class RunLog
{
    [LoggerMessage(EventId = 1101, Level = LogLevel.Error, Message = "Agent run {RunId} failed with an unexpected error")]
    public static partial void RunFailed(ILogger logger, Guid runId, Exception exception);

    [LoggerMessage(EventId = 1102, Level = LogLevel.Error, Message = "Agent run {RunId} could not record its end")]
    public static partial void EndFailed(ILogger logger, Guid runId, Exception exception);

    [LoggerMessage(EventId = 1103, Level = LogLevel.Warning, Message = "Agent run {RunId}: step {Tool} failed with an unexpected error")]
    public static partial void StepFailed(ILogger logger, Guid runId, string tool, Exception exception);

    [LoggerMessage(EventId = 1104, Level = LogLevel.Warning, Message = "Agent run {RunId}: the risk check of {Tool} failed; the step needs approval")]
    public static partial void RiskCheckFailed(ILogger logger, Guid runId, string tool, Exception exception);
}
