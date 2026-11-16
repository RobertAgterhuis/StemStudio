using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.Model;

/// <summary>State rules of data-model 4 and 7 that every owner relies on (pure functions).</summary>
public static class AgentStateRules
{
    /// <summary>True for completed, failed, cancelled and interrupted runs (final states).</summary>
    /// <param name="status">Run status.</param>
    /// <returns>True when the run can no longer change.</returns>
    public static bool IsTerminal(AgentRunStatusDto status) => status is AgentRunStatusDto.Completed
        or AgentRunStatusDto.Failed or AgentRunStatusDto.Cancelled or AgentRunStatusDto.Interrupted;

    /// <summary>True for step states that no longer change (except <c>succeeded</c> -> <c>undone</c>).</summary>
    /// <param name="status">Step status.</param>
    /// <returns>True when the step is finished.</returns>
    public static bool IsTerminal(AgentStepStatusDto status) => status is AgentStepStatusDto.Succeeded
        or AgentStepStatusDto.Failed or AgentStepStatusDto.Rejected or AgentStepStatusDto.RejectedInvalid
        or AgentStepStatusDto.Skipped or AgentStepStatusDto.Expired or AgentStepStatusDto.Undone;

    /// <summary>True for the risks that always need the user's approval (ADR-020 D5).</summary>
    /// <param name="risk">Risk class.</param>
    /// <returns>True for heavy and destructive.</returns>
    public static bool AlwaysRequiresApproval(ToolRiskDto risk) => risk is ToolRiskDto.Heavy or ToolRiskDto.Destructive;

    /// <summary>True for the risks that need approval once the run is tainted or a provenance check fails.</summary>
    /// <param name="risk">Risk class.</param>
    /// <returns>True for change and job.</returns>
    public static bool RequiresApprovalWhenTainted(ToolRiskDto risk) => risk is ToolRiskDto.Change or ToolRiskDto.Job;
}
