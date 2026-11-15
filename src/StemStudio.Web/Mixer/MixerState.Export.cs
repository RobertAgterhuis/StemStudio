using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Export;

namespace StemStudio.Web.Mixer;

/// <summary>Export snapshot of <see cref="MixerState"/> (feature 007, FR-002): the current, possibly unsaved state.</summary>
public sealed partial class MixerState
{
    /// <summary>
    /// The target of a mixer export: the mix as it sounds now, every visible channel in mixer order (standard stems, then
    /// detail stems) and the audible ones (the gate rule the engine applies), the loop region and the duration.
    /// </summary>
    /// <param name="track">The loaded track.</param>
    /// <returns>The target.</returns>
    public ExportTarget ToExportTarget(TrackDto track)
    {
        var settings = ToSettings(string.Empty);
        var visible = AllChannels.Where(c => !IsHidden(c)).ToList();
        return new ExportTarget(ExportKind.Mix, track)
        {
            Mix = new ExportMixDto(settings.MasterVolume, settings.Channels) { Expanded = settings.Expanded, DetailChannels = settings.DetailChannels },
            Channels = [.. visible.Select(c => new ExportChannel(c.Stem, c.Stem.Replace(".", ": ", StringComparison.Ordinal)))],
            Audible = [.. visible.Where(IsAudible).Select(c => c.Stem)],
            Loop = Loop,
            Duration = Duration > 0 ? Duration : track.DurationSeconds ?? 0,
        };
    }
}
