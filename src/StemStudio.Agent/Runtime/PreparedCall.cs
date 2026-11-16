using System.Text.Json;
using Microsoft.Extensions.AI;
using StemStudio.Agent.Model;
using StemStudio.Agent.Tools;
using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.Runtime;

/// <summary>A model call after registry lookup and schema validation (FR-005, FR-008).</summary>
/// <param name="Call">The model's call.</param>
/// <param name="Tool">The offered tool; null for unknown or not-offered names.</param>
/// <param name="Validation">Argument validation (canonical JSON and hash also for invalid calls).</param>
internal sealed record PreparedCall(FunctionCallContent Call, AgentToolDescriptor? Tool, ArgumentValidation Validation)
{
    private const int CallIdMaxLength = 100;

    /// <summary>True when the tool is offered and the arguments are valid.</summary>
    public bool IsValid => Tool is not null && Validation.IsValid;

    /// <summary>Readable reason for an invalid call.</summary>
    public string Error => Tool is null
        ? $"Unknown tool '{Call.Name}': use one of the offered tools."
        : $"Invalid arguments for {Tool.Name}: {Validation.Error}";

    /// <summary>Looks the call up among the offered tools and validates its arguments.</summary>
    public static PreparedCall From(FunctionCallContent call, IReadOnlyList<AgentToolDescriptor> offered)
    {
        var tool = offered.FirstOrDefault(item => item.Name == call.Name);
        var json = call.Arguments is null ? null : JsonSerializer.Serialize(call.Arguments);
        return new PreparedCall(call, tool, ToolArgumentValidator.Validate(tool?.Schema ?? ToolSchema.Empty, json));
    }

    /// <summary>The step record of the call.</summary>
    public RunStep ToStep(Guid runId, int seq, AgentStepStatusDto status) => new()
    {
        Id = Guid.NewGuid(),
        RunId = runId,
        Seq = seq,
        CallId = Call.CallId.Length > CallIdMaxLength ? Call.CallId[..CallIdMaxLength] : Call.CallId,
        Tool = Call.Name,
        Kind = Tool?.Kind ?? ToolKindDto.Server,
        Risk = Tool?.Risk ?? ToolRiskDto.Read,
        UndoKind = Tool?.UndoKind ?? UndoKindDto.None,
        Arguments = Validation.Canonical,
        ArgumentsHash = Validation.Hash,
        Decision = StepDecisionDto.Auto,
        Status = status,
    };
}
