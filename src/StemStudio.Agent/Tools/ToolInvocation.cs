using System.Text.Json;
using System.Text.Json.Nodes;
using StemStudio.Agent.Model;
using StemStudio.Agent.Ports;
using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.Tools;

/// <summary>Executes a server tool with validated arguments.</summary>
/// <param name="invocation">The call.</param>
/// <param name="cancellationToken">Cancellation.</param>
/// <returns>The result (projection, job, undo action).</returns>
public delegate Task<ToolResult> ToolHandler(ToolInvocation invocation, CancellationToken cancellationToken);

/// <summary>Estimates a heavy call before it is shown on an approval card.</summary>
/// <param name="invocation">The call.</param>
/// <param name="cancellationToken">Cancellation.</param>
/// <returns>The estimate.</returns>
public delegate Task<ToolEstimate> ToolEstimator(ToolInvocation invocation, CancellationToken cancellationToken);

/// <summary>Maps the result a UI tool returned to the projection and undo action.</summary>
/// <param name="arguments">Validated arguments.</param>
/// <param name="result">The page's result object, when any.</param>
/// <returns>The tool result.</returns>
public delegate ToolResult UiResultMapper(JsonElement arguments, JsonElement? result);

/// <summary>Asks the open page to execute a UI tool (used by server tools that need the open mixer's state).</summary>
/// <param name="tool">UI tool name.</param>
/// <param name="arguments">Canonical JSON arguments.</param>
/// <param name="cancellationToken">Cancellation.</param>
/// <returns>The page's result.</returns>
public delegate Task<UiToolResult> UiRequester(string tool, string arguments, CancellationToken cancellationToken);

/// <summary>One validated call handed to a handler.</summary>
public sealed record ToolInvocation
{
    /// <summary>Validated arguments (an object).</summary>
    public required JsonElement Arguments { get; init; }

    /// <summary>StemStudio's public API over loopback.</summary>
    public required IStemStudioActions Actions { get; init; }

    /// <summary>Page context of the command.</summary>
    public required PageContextDto Context { get; init; }

    /// <summary>Backend of the run (Claude omits biographies, constitution 1.3.0 IV item 5).</summary>
    public AgentBackendDto Backend { get; init; } = AgentBackendDto.Ollama;

    /// <summary>Asks the open page for UI state; null when no UI is connected.</summary>
    public UiRequester? Ui { get; init; }

    /// <summary>A string argument or null.</summary>
    /// <param name="name">Argument name.</param>
    /// <returns>The value.</returns>
    public string? Text(string name) => Arguments.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <summary>A GUID argument (validated as uuid by the schema).</summary>
    /// <param name="name">Argument name.</param>
    /// <returns>The value or null.</returns>
    public Guid? Id(string name) => Guid.TryParse(Text(name), out var id) ? id : null;

    /// <summary>A required GUID argument.</summary>
    /// <param name="name">Argument name.</param>
    /// <returns>The value.</returns>
    public Guid RequiredId(string name) => Id(name) ?? throw new AgentActionException(400, "validation", $"Argument {name} is required.");

    /// <summary>A number argument or null.</summary>
    /// <param name="name">Argument name.</param>
    /// <returns>The value.</returns>
    public double? Number(string name) => Arguments.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetDouble() : null;

    /// <summary>An integer argument or null.</summary>
    /// <param name="name">Argument name.</param>
    /// <returns>The value.</returns>
    public int? Whole(string name) => Number(name) is { } number ? (int)number : null;

    /// <summary>A boolean argument or null.</summary>
    /// <param name="name">Argument name.</param>
    /// <returns>The value.</returns>
    public bool? Flag(string name) => Arguments.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : null;

    /// <summary>A string array argument (empty when absent).</summary>
    /// <param name="name">Argument name.</param>
    /// <returns>The values.</returns>
    public IReadOnlyList<string> Strings(string name) => Arguments.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
        ? [.. value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString()!)]
        : [];
}

/// <summary>What a tool call produced.</summary>
public sealed record ToolResult
{
    /// <summary>The compact projection the model sees (whitelisted fields).</summary>
    public required JsonNode Projection { get; init; }

    /// <summary>The job the call started, for wait_for_job.</summary>
    public JobRef? Job { get; init; }

    /// <summary>Estimated job duration in minutes.</summary>
    public double? EstimatedMinutes { get; init; }

    /// <summary>The inverse or compensating call recorded for undo.</summary>
    public UndoAction? Undo { get; init; }

    /// <summary>Further inverse calls of the same step (e.g. one per channel of a mixer reset).</summary>
    public IReadOnlyList<UndoAction> MoreUndo { get; init; } = [];

    /// <summary>Every inverse or compensating call of the step, in recording order.</summary>
    public IReadOnlyList<UndoAction> AllUndo => Undo is null ? MoreUndo : [Undo, .. MoreUndo];

    /// <summary>Final job progress 0-100 (wait_for_job), when any.</summary>
    public int? Progress { get; init; }

    /// <summary>True when <see cref="Undo"/> compensates an artefact created by this call (no approval needed while fresh).</summary>
    public bool UndoFresh { get; init; }

    /// <summary>A result with only a projection.</summary>
    /// <param name="projection">The projection.</param>
    /// <returns>The result.</returns>
    public static ToolResult Of(JsonNode projection) => new() { Projection = projection };
}

/// <summary>Estimate of a heavy call for its approval card.</summary>
/// <param name="Effect">Readable effect, e.g. "Detail separation of Hero of the Day".</param>
/// <param name="EstimatedMinutes">Estimated minutes, when known.</param>
/// <param name="Device">Device text, e.g. "GPU (RTX 4070)".</param>
public sealed record ToolEstimate(string Effect, double? EstimatedMinutes = null, string? Device = null)
{
    /// <summary>The device as the approval card carries it: <c>gpu</c> or <c>cpu</c> (null when unknown).</summary>
    public string? DeviceCode => Device is null ? null : Device.StartsWith("gpu", StringComparison.OrdinalIgnoreCase) ? "gpu" : "cpu";
}
