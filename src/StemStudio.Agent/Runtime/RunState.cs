using System.Text;
using Microsoft.Extensions.AI;
using StemStudio.Agent.Model;
using StemStudio.Agent.Policy;
using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.Runtime;

/// <summary>The in-memory state of one run execution (the persisted part is <see cref="Run"/>).</summary>
internal sealed class RunState
{
    /// <summary>The persisted run (replaced on every change).</summary>
    public required AgentRun Run { get; set; }

    /// <summary>The user's command.</summary>
    public required ConversationMessage Command { get; init; }

    /// <summary>Messages before the command, in Seq order.</summary>
    public required IReadOnlyList<ConversationMessage> History { get; init; }

    /// <summary>Texts of every user message of the conversation (provenance).</summary>
    public required IReadOnlyList<string> UserMessages { get; init; }

    /// <summary>Limits of the run.</summary>
    public required AgentLimitsDto Limits { get; init; }

    /// <summary>Current page (changes after open_mixer, open_track and navigate).</summary>
    public required string Page { get; set; }

    /// <summary>Assistant turns and tool results of this run, in order.</summary>
    public List<ChatMessage> Transcript { get; } = [];

    /// <summary>Plan-scoped approvals granted in this run.</summary>
    public List<PlanHeavyGrant> Grants { get; } = [];

    /// <summary>Assistant text of every turn.</summary>
    public StringBuilder Answer { get; } = new();

    /// <summary>Last step Seq.</summary>
    public int StepSeq { get; set; }

    /// <summary>Model of the previous turn; null before the first turn.</summary>
    public string? Model { get; set; }

    /// <summary>The run's id.</summary>
    public Guid RunId => Run.Id;

    /// <summary>Page context of the current page.</summary>
    public PageContextDto Context => Command.Context with { Page = Page };
}

/// <summary>Ends a run with a failure code (limits, backend, timeout).</summary>
internal sealed class RunStopException : Exception
{
    public RunStopException(string code, string message)
        : base(message) => Code = code;

    public RunStopException()
        : this(AgentFailureCodes.Internal, "The run stopped.")
    {
    }

    public RunStopException(string message)
        : this(AgentFailureCodes.Internal, message)
    {
    }

    public RunStopException(string message, Exception innerException)
        : base(message, innerException) => Code = AgentFailureCodes.Internal;

    /// <summary>Failure code (AgentFailureCodes).</summary>
    public string Code { get; }
}
