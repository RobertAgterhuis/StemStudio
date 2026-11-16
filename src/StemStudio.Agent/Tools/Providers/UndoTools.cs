using StemStudio.Contracts.Agent;
using static StemStudio.Agent.Tools.Providers.ToolDefinitions;

namespace StemStudio.Agent.Tools.Providers;

/// <summary>Tools that only undo runs execute (not in the registry, never offered to the model).</summary>
public static class UndoTools
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
        Tool("011", "remove_lyrics", ToolKindDto.Server, ToolRiskDto.Destructive) with
        {
            Title = "Remove fetched lyrics",
            Description = "Undo of fetch_lyrics.",
            Schema = ToolSchema.Of(Param.Uuid("trackId")),
            Handler = LyricsToolProvider.RemoveAsync,
        },
        Tool("011", GenerationTools.DiscardTool, ToolKindDto.Server, ToolRiskDto.Destructive) with
        {
            Title = "Discard a generation",
            Description = "Undo of restyle_track, generate_song and convert_take_voice: cancel while active, then delete.",
            Schema = ToolSchema.Of(Param.Uuid("generationId")),
            TargetArgument = "generationId",
            Handler = GenerationTools.DiscardAsync,
        },
    ];
}
