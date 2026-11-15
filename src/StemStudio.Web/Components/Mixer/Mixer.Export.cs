using StemStudio.Web.Export;

namespace StemStudio.Web.Components.Mixer;

/// <summary>"Save to library" of the mixer (feature 007, US2): exports the current, possibly unsaved state.</summary>
public sealed partial class Mixer
{
    /// <summary>The open export dialog's target, or null.</summary>
    private ExportTarget? Exporting { get; set; }

    private void OpenExport() => Exporting = State!.ToExportTarget(_track!);

    private void CloseExport() => Exporting = null;
}
