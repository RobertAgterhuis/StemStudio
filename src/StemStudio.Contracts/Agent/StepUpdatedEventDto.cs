using System.Text.Json;

namespace StemStudio.Contracts.Agent;

/// <summary>Data of the step.updated event.</summary>
public sealed record StepUpdatedEventDto
{
    /// <summary>Step identifier.</summary>
    public required Guid StepId { get; init; }

    /// <summary>New status.</summary>
    public required AgentStepStatusDto Status { get; init; }

    /// <summary>Job progress 0-100.</summary>
    public int? Progress { get; init; }

    /// <summary>Projection the model saw.</summary>
    public JsonElement? ResultSummary { get; init; }

    /// <summary>Readable error.</summary>
    public string? Error { get; init; }
}
