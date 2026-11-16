using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.Tools.Providers;

/// <summary>The 011 meta tools <c>set_plan</c> and <c>wait_for_job</c> (tool catalogue 1.0.0); the runner executes them.</summary>
public sealed class MetaToolProvider : IAgentToolProvider
{
    /// <summary>Name of the plan tool.</summary>
    public const string SetPlan = "set_plan";

    /// <summary>Name of the job wait tool.</summary>
    public const string WaitForJob = "wait_for_job";

    private static readonly string[] Phases = ["standard", "detail", "lyrics", "export", "transcription", "restyle", "generation"];

    /// <inheritdoc />
    public IEnumerable<AgentToolDescriptor> GetTools() =>
    [
        Meta(SetPlan, "Publish the plan", "Publish the plan of a command with more than one action before the first action. Calling it again replaces the steps that have not started.",
            ToolSchema.Of(Param.List("steps", Param.Record("step", Param.Text("title", 1, 120), Param.Text("tool", 1, 60).Optional()), 1, 10))),
        Meta(WaitForJob, "Wait for a job", "Wait until a background job finished (separation, detail, lyrics, export, transcription, restyle, generation). Use only when a later step needs the result. Pass trackId for track phases, jobId for export, transcription, restyle and generation.",
            ToolSchema.Of(Param.Choice("phase", Phases), Param.Uuid("trackId").Optional(), Param.Text("jobId", 1, 100).Optional()).AtLeastOne("trackId", "jobId")),
    ];

    private static AgentToolDescriptor Meta(string name, string title, string description, ToolSchema schema) => new()
    {
        Name = name,
        Feature = "011",
        Kind = ToolKindDto.Meta,
        Risk = ToolRiskDto.Read,
        UndoKind = UndoKindDto.None,
        Group = ToolGroup.Core,
        Availability = ToolAvailability.Always,
        Title = title,
        Description = description,
        Schema = schema,
    };
}
