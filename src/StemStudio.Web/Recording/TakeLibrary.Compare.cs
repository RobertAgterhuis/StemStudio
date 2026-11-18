using StemStudio.Contracts.Recording;

namespace StemStudio.Web.Recording;

/// <summary>Compare mode and mix restore of <see cref="TakeLibrary"/> (FR-012, FR-013, US4).</summary>
public sealed partial class TakeLibrary
{
    private static readonly TakeCompareMode[] Cycle = [TakeCompareMode.Both, TakeCompareMode.Take, TakeCompareMode.Original];

    /// <summary>The compare mode of the loaded take (engine D8: kept on load, reset to Both on unload).</summary>
    public TakeCompareMode Compare { get; private set; } = TakeCompareMode.Both;

    /// <summary>True when the loaded take has no part: only the take on/off switch applies.</summary>
    public bool CompareIsToggle => Loaded?.Part == RecordingPartDto.None;

    /// <summary>Sets the compare mode (sample-locked in the engine; user mute/solo stay untouched).</summary>
    /// <param name="mode">The mode.</param>
    /// <returns>A task that completes when delivered.</returns>
    public async Task SetCompareAsync(TakeCompareMode mode)
    {
        if (Loaded is null)
        {
            return;
        }

        Compare = mode;
        try
        {
            await engine.SetTakeCompareAsync(mode);
        }
        catch (RecordingException ex)
        {
            Error = ex.Message;
        }
    }

    /// <summary>The <c>C</c> shortcut: Both -> My take -> Original -> Both; with part none, take on/off.</summary>
    /// <returns>A task that completes when delivered.</returns>
    public Task CycleCompareAsync() => SetCompareAsync(CompareIsToggle
        ? (Compare == TakeCompareMode.Both ? TakeCompareMode.Original : TakeCompareMode.Both)
        : Cycle[(Array.IndexOf(Cycle, Compare) + 1) % Cycle.Length]);

    /// <summary>Restores the take strip of a saved mix (FR-012): loads the take and applies its settings; a missing take is ignored.</summary>
    /// <param name="settings">The mix's take channel, or null.</param>
    /// <returns>A task that completes when the take is scheduled.</returns>
    public async Task RestoreAsync(TakeChannelSettingsDto? settings)
    {
        if (settings is null || _items.FirstOrDefault(i => i.Take?.Id == settings.TakeId) is not { } item)
        {
            return;
        }

        (Strip.Volume, Strip.Pan, Strip.Mute, Strip.Solo) = (settings.Volume, settings.Pan, settings.Mute, settings.Solo);
        await LoadIntoStripAsync(item);
    }
}
