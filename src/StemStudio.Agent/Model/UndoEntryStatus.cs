namespace StemStudio.Agent.Model;

/// <summary>Status of an undo entry (data-model 4.4); internal to the agent, not part of the HTTP contract.</summary>
public enum UndoEntryStatus
{
    /// <summary>Can be applied by the next undo.</summary>
    Available,

    /// <summary>Applied by an undo run.</summary>
    Applied,

    /// <summary>The target changed outside the agent or no longer exists.</summary>
    Obsolete,
}
