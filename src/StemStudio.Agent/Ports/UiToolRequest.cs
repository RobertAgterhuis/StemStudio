namespace StemStudio.Agent.Ports;

/// <summary>A UI tool call for the open page (contracts/DELTA.md section 4).</summary>
public sealed record UiToolRequest
{
    /// <summary>Run identifier.</summary>
    public required Guid RunId { get; init; }

    /// <summary>Step identifier (path of the ui-results POST).</summary>
    public required Guid StepId { get; init; }

    /// <summary>UI tool name.</summary>
    public required string Tool { get; init; }

    /// <summary>Canonical JSON arguments.</summary>
    public required string Arguments { get; init; }

    /// <summary>Time the page has to answer (15 s).</summary>
    public required TimeSpan Timeout { get; init; }
}
