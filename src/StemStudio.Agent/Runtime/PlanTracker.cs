using StemStudio.Agent.Model;
using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.Runtime;

/// <summary>One step of a <c>set_plan</c> call.</summary>
/// <param name="Title">Title, at most 120 characters.</param>
/// <param name="Tool">Expected tool, when known.</param>
public sealed record PlanStep(string Title, string? Tool);

/// <summary>The plan after linking a call.</summary>
/// <param name="Plan">The plan.</param>
/// <param name="Index">The linked item; null without a plan.</param>
public sealed record PlanLink(IReadOnlyList<PlanItem> Plan, int? Index);

/// <summary>
/// Plan rules (FR-007): <c>set_plan</c> replaces the not-yet-started items; calls are linked to the first pending item of
/// their tool, unplanned calls are appended as extra items; without a plan nothing is linked.
/// </summary>
public static class PlanTracker
{
    /// <summary>Replaces the pending items with new steps; started items stay.</summary>
    /// <param name="plan">The current plan.</param>
    /// <param name="steps">The new steps.</param>
    /// <returns>The new plan.</returns>
    public static IReadOnlyList<PlanItem> Replace(IReadOnlyList<PlanItem> plan, IReadOnlyList<PlanStep> steps)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(steps);
        var kept = plan.Where(item => item.Status != PlanItemStatusDto.Pending).ToList();
        var added = steps.Select((step, offset) => new PlanItem { Index = kept.Count + offset, Title = step.Title, Tool = step.Tool, Status = PlanItemStatusDto.Pending });
        return [.. kept.Select((item, index) => item with { Index = index }), .. added];
    }

    /// <summary>Links a call to the plan and marks the item running.</summary>
    /// <param name="plan">The current plan.</param>
    /// <param name="tool">Tool of the call.</param>
    /// <param name="title">Title for an extra item.</param>
    /// <returns>The plan and the linked index.</returns>
    public static PlanLink Link(IReadOnlyList<PlanItem> plan, string tool, string title)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.Count == 0)
        {
            return new PlanLink(plan, null);
        }

        var match = plan.FirstOrDefault(item => item.Status == PlanItemStatusDto.Pending && item.Tool == tool);
        if (match is null)
        {
            var extra = new PlanItem { Index = plan.Count, Title = title, Tool = tool, Status = PlanItemStatusDto.Running };
            return new PlanLink([.. plan, extra], extra.Index);
        }

        return new PlanLink(SetStatus(plan, match.Index, PlanItemStatusDto.Running), match.Index);
    }

    /// <summary>Sets the status of one item.</summary>
    /// <param name="plan">The plan.</param>
    /// <param name="index">Item index; null changes nothing.</param>
    /// <param name="status">New status.</param>
    /// <returns>The plan.</returns>
    public static IReadOnlyList<PlanItem> SetStatus(IReadOnlyList<PlanItem> plan, int? index, PlanItemStatusDto status)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return index is null ? plan : [.. plan.Select(item => item.Index == index ? item with { Status = status } : item)];
    }
}
