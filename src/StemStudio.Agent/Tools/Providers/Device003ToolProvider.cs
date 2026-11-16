using System.Text.Json.Nodes;
using StemStudio.Contracts.Agent;
using static StemStudio.Agent.Tools.Providers.ToolDefinitions;

namespace StemStudio.Agent.Tools.Providers;

/// <summary>The processing device tool of feature 003 (tool catalogue 1.0.0).</summary>
public sealed class Device003ToolProvider : IAgentToolProvider
{
    /// <inheritdoc />
    public IEnumerable<AgentToolDescriptor> GetTools() =>
    [
        Tool("003", "get_processing_device", ToolKindDto.Server, ToolRiskDto.Read) with
        {
            Title = "Read the processing device",
            Description = "Tell whether separation runs on the GPU or the CPU, and why when the GPU is not used.",
            Handler = GetDeviceAsync,
        },
    ];

    private static async Task<ToolResult> GetDeviceAsync(ToolInvocation call, CancellationToken cancellationToken)
    {
        var device = await call.Actions.Tracks.GetDeviceAsync(cancellationToken);
        return ToolResult.Of(new JsonObject
        {
            ["reachable"] = device.Reachable,
            ["kind"] = device.Kind is { } kind ? Name(kind) : null,
            ["name"] = device.Name,
            ["reason"] = device.Reason,
        });
    }
}
