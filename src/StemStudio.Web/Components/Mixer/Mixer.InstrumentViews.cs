using StemStudio.Contracts.InstrumentViews;
using StemStudio.Web.Audio;
using StemStudio.Web.InstrumentViews;

namespace StemStudio.Web.Components.Mixer;

/// <summary>Instrument views of the mixer page (feature 016): the chord analysis shared by the chord lane, the banner and the key.</summary>
public sealed partial class Mixer
{
    [Microsoft.AspNetCore.Components.Inject]
    private IInstrumentViewEngine Views { get; set; } = default!;

    /// <summary>The last loaded chord analysis, or null before the first answer.</summary>
    internal TrackAnalysisDto<ChordChartDocumentDto>? ChordAnalysis { get; private set; }

    /// <summary>The ready chart for the karaoke banner, or null.</summary>
    private ChordChartData? BannerChords { get; set; }

    private void OnChordsLoaded(TrackAnalysisDto<ChordChartDocumentDto> analysis)
    {
        var document = analysis.Status == AnalysisStatusDto.Ready ? analysis.Document : null;
        if (ChordsChanged(document, analysis.Status))
        {
            BannerChords = document is { NoChords: false } ? ChordLabels.ToEngine(document) : null;
        }

        ChordAnalysis = analysis;
    }

    private bool ChordsChanged(ChordChartDocumentDto? document, AnalysisStatusDto status) =>
        !ReferenceEquals(document, ChordAnalysis?.Document) || ChordAnalysis?.Status != status;

    // Capo mode (US8): the engine re-spells the lane and the banner for the capo (setChordDisplay).
    private Task SetCapoAsync(CapoSetting setting) =>
        EngineViewsAsync(() => Views.SetChordDisplayAsync(new ChordDisplay(setting.Effective, setting.On && setting.ShowSounding)));

    private async Task EngineViewsAsync(Func<Task> call)
    {
        try
        {
            await call();
        }
        catch (AudioEngineException ex)
        {
            EngineError = ex.Message;
        }
    }
}
