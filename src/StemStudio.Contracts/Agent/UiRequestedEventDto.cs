using System.Text.Json;

namespace StemStudio.Contracts.Agent;

/// <summary>Data of the ui.requested event: the open page executes the tool and posts the result.</summary>
public sealed record UiRequestedEventDto
{
    /// <summary>Step identifier (path of the ui-results POST).</summary>
    public required Guid StepId { get; init; }

    /// <summary>UI tool name.</summary>
    public required string Tool { get; init; }

    /// <summary>Canonical arguments.</summary>
    public required JsonElement Arguments { get; init; }

    /// <summary>Seconds until the step fails with "The page did not respond" (15).</summary>
    public required int TimeoutSeconds { get; init; }
}
