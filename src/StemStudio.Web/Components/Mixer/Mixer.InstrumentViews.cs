using StemStudio.Contracts.InstrumentViews;
using StemStudio.Web.Audio;
using StemStudio.Web.InstrumentViews;

namespace StemStudio.Web.Components.Mixer;

/// <summary>Instrument views of the mixer page (feature 016): the chord analysis shared by the chord lane, the banner and the key.</summary>
public sealed partial class Mixer
{
    /// <summary>The last loaded chord analysis, or null before the first answer.</summary>
    internal TrackAnalysisDto<ChordChartDocumentDto>? ChordAnalysis { get; private set; }

    /// <summary>The ready chart for the karaoke banner, or null.</summary>
    private ChordChartData? BannerChords { get; set; }

    private void OnChordsLoaded(TrackAnalysisDto<ChordChartDocumentDto> analysis)
    {
        var document = analysis.Status == AnalysisStatusDto.Ready ? analysis.Document : null;
        if (!ReferenceEquals(document, ChordAnalysis?.Document) || ChordAnalysis?.Status != analysis.Status)
        {
            BannerChords = document is { NoChords: false } ? ChordLabels.ToEngine(document) : null;
        }

        ChordAnalysis = analysis;
    }
}
