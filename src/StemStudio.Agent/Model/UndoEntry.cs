using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.Model;

/// <summary>One recorded undo action; one user command forms one undo group (data-model 4.4).</summary>
public sealed record UndoEntry
{
    /// <summary>Entry identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>Conversation identifier.</summary>
    public required Guid ConversationId { get; init; }

    /// <summary>Run (undo group) identifier.</summary>
    public required Guid RunId { get; init; }

    /// <summary>Step that is reversed.</summary>
    public required Guid StepId { get; init; }

    /// <summary>inverse or compensate.</summary>
    public required UndoKindDto Kind { get; init; }

    /// <summary>The call that reverses the step.</summary>
    public required UndoAction Action { get; init; }

    /// <summary>True for a compensation of an artefact created by this run and not modified since (no approval).</summary>
    public required bool Fresh { get; init; }

    /// <summary>Entry status.</summary>
    public required UndoEntryStatus Status { get; init; }
}
