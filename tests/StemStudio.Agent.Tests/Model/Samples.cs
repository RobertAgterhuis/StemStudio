using StemStudio.Agent.Model;
using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.Tests.Model;

/// <summary>Sample records for the skeleton tests.</summary>
internal static class Samples
{
    public static readonly DateTimeOffset Now = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);

    public static AgentRun Run(AgentRunStatusDto status) => new()
    {
        Id = Guid.NewGuid(),
        ConversationId = Guid.NewGuid(),
        UserMessageId = Guid.NewGuid(),
        Status = status,
        Backend = AgentBackendDto.Fake,
        Model = "scripted",
        Tainted = false,
        ModelTurns = 0,
        ToolCalls = 0,
        InvalidCalls = 0,
        StartedAt = Now,
    };

    public static Approval Approval(ApprovalStatusDto status, DateTimeOffset requestedAt) => new()
    {
        Id = Guid.NewGuid(),
        RunId = Guid.NewGuid(),
        Status = status,
        Scope = ApprovalScopeDto.Once,
        ArgumentsHash = new string('a', 64),
        RequestedAt = requestedAt,
    };
}
