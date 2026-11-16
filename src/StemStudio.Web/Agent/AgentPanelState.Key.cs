namespace StemStudio.Web.Agent;

/// <summary>
/// The note "Key changed - applies to the next command" (011 follow-up claude-key, CK-FR-016): an active run keeps the
/// Claude client it started with, so a key stored, replaced or deleted during it only applies to the next command.
/// </summary>
public sealed partial class AgentPanelState
{
    private Guid? _keyChangedRun;

    /// <summary>True while the run that was active when the Claude key changed is still active.</summary>
    public bool KeyChangedDuringRun => Run is { Ended: false } run && run.RunId == _keyChangedRun;

    /// <summary>Records a Claude key change; noted only when a run is active (the note ends with that run).</summary>
    public void NoteKeyChanged()
    {
        if (Run is { Ended: false } run)
        {
            _keyChangedRun = run.RunId;
            Notify();
        }
    }
}
