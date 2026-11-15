namespace StemStudio.Agent.Model;

/// <summary>The inverse or compensating call recorded for undo.</summary>
public sealed record UndoAction
{
    /// <summary>Tool to call, e.g. mixer_set_channel with the previous values or delete_mix for a fresh mix.</summary>
    public required string Tool { get; init; }

    /// <summary>Canonical JSON arguments.</summary>
    public required string Arguments { get; init; }
}
