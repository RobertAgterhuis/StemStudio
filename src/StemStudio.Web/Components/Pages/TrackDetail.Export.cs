using StemStudio.Web.Export;

namespace StemStudio.Web.Components.Pages;

/// <summary>Export actions of the track detail page (feature 007, US1, US5).</summary>
public sealed partial class TrackDetail
{
    /// <summary>The open export dialog's target, or null.</summary>
    private ExportTarget? Exporting { get; set; }

    private void OpenExport(string channel) => Exporting = ExportTarget.ForStem(Track!, channel);

    private void OpenStemPack() => Exporting = ExportTarget.ForStemPack(Track!);

    private void CloseExport() => Exporting = null;
}
