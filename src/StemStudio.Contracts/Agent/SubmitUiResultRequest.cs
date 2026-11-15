using System.Text.Json;

namespace StemStudio.Contracts.Agent;

/// <summary>Body of POST /api/agent/runs/{runId}/ui-results/{stepId} (UI tool round trip).</summary>
public sealed record SubmitUiResultRequest
{
    /// <summary>True when the page executed the tool.</summary>
    public required bool Ok { get; init; }

    /// <summary>Result object, at most 4 KB.</summary>
    public JsonElement? Result { get; init; }

    /// <summary>Readable error, at most 500 characters.</summary>
    public string? Error { get; init; }
}
