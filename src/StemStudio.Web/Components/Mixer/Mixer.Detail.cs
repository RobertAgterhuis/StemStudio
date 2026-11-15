using StemStudio.Web.Audio;
using StemStudio.Web.Mixer;

namespace StemStudio.Web.Components.Mixer;

/// <summary>
/// Expanding and collapsing detail stem groups (FR-012, FR-015): the engine swaps first, then the state follows.
/// The page remembers which parents the engine has expanded (all collapsed after <c>loadTrack</c>), so a mix load
/// sends <c>setExpanded</c> only for parents whose state changes, always before the channel settings.
/// </summary>
public sealed partial class Mixer
{
    private readonly HashSet<string> _expanding = new(StringComparer.Ordinal);
    private readonly HashSet<string> _engineExpanded = new(StringComparer.Ordinal);

    private bool IsExpanding(string parent) => _expanding.Contains(parent);

    private async Task ToggleExpandAsync(string parent)
    {
        if (!EngineReady || !_expanding.Add(parent))
        {
            return;
        }

        try
        {
            await SwapAsync(parent, !State!.IsExpanded(parent));
        }
        finally
        {
            _expanding.Remove(parent);
        }
    }

    private async Task SwapAsync(string parent, bool expanded)
    {
        if (await TryExpandAsync(State!, parent, expanded))
        {
            State!.SetExpanded(parent, expanded);
            EngineError = null;
        }
    }

    private async Task PushExpansionAsync(MixerState state)
    {
        foreach (var group in state.Groups.Where(g => g.Expanded != _engineExpanded.Contains(g.Parent)))
        {
            await TryExpandAsync(state, group.Parent, group.Expanded);
        }
    }

    private async Task<bool> TryExpandAsync(MixerState state, string parent, bool expanded)
    {
        try
        {
            await Engine.SetExpandedAsync(parent, expanded);
            _ = expanded ? _engineExpanded.Add(parent) : _engineExpanded.Remove(parent);
            return true;
        }
        catch (AudioEngineException ex)
        {
            state.RejectExpanded(parent);
            EngineError = ex.Message;
            return false;
        }
    }
}
