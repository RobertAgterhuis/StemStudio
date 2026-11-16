using StemStudio.Contracts.Agent;
using static StemStudio.Agent.Tools.Providers.ToolDefinitions;

namespace StemStudio.Agent.Tools.Providers;

/// <summary>Tools that only undo runs execute (not in the registry, never offered to the model).</summary>
internal static class UndoTools
{
    /// <summary>The undo-only tools.</summary>
    public static IReadOnlyList<AgentToolDescriptor> All { get; } =
    [
        Tool("011", Core001Handlers.RestoreMixTool, ToolKindDto.Server, ToolRiskDto.Change) with
        {
            Title = "Restore the previous mix version",
            Description = "Undo of update_mix.",
            Handler = Core001Handlers.RestoreMixAsync,
        },
    ];
}
