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

    /// <summary>The chord display the engine was last given (capo mode of the guitar family), or null for the default.</summary>
    private ChordDisplay? CapoDisplay { get; set; }

    // Capo mode (US8): the engine re-spells the lane and the banner for the capo (setChordDisplay).
    private Task SetCapoAsync(CapoSetting setting) => SendChordDisplayAsync(new ChordDisplay(setting.Effective, setting.On && setting.ShowSounding));

    // The chord display is engine-wide while capo mode belongs to the guitar family: another family resets it (review M1).
    private Task ResetCapoAsync(InstrumentFamilyDto family) =>
        family != InstrumentFamilyDto.Guitar && CapoDisplay is not null ? SendChordDisplayAsync(new ChordDisplay(0, false)) : Task.CompletedTask;

    private Task SendChordDisplayAsync(ChordDisplay display)
    {
        CapoDisplay = display == new ChordDisplay(0, false) ? null : display;
        return EngineViewsAsync(() => Views.SetChordDisplayAsync(display));
    }

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
