namespace StemStudio.Web.Agent;

/// <summary>
/// Tells the pages of a circuit that an agent run has ended, so lists of things the agent may have created or changed
/// (for example the track page's restyles) reload instead of waiting for the next visit.
/// </summary>
public sealed class AgentRunNotifier
{
    /// <summary>Raised after an agent run of this circuit has ended.</summary>
    public event Action? RunEnded;

    /// <summary>Raises <see cref="RunEnded"/>.</summary>
    public void RunHasEnded() => RunEnded?.Invoke();
}
