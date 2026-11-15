using StemStudio.Web.Audio;

namespace StemStudio.Web.Components.Mixer;

/// <summary>Expanding and collapsing detail stem groups (FR-012): the engine swaps first, then the state follows.</summary>
public sealed partial class Mixer
{
    private readonly HashSet<string> _expanding = new(StringComparer.Ordinal);

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
        try
        {
            await Engine.SetExpandedAsync(parent, expanded);
            State!.SetExpanded(parent, expanded);
            EngineError = null;
        }
        catch (AudioEngineException ex)
        {
            State!.RejectExpanded(parent);
            EngineError = ex.Message;
        }
    }
}
