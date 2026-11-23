using Microsoft.AspNetCore.Components;
using StemStudio.Web.Audio;

namespace StemStudio.Web.Components.Mixer.Karaoke;

/// <summary>
/// Banner chords (016 US1, FR-013): the mixer passes the ready chord chart; the engine draws <c>.karaoke-chord</c>
/// elements above the words (<c>setKaraokeChords</c>, allowed before or after <c>attachKaraoke</c>). The host then carries
/// <c>data-chords="true"</c>; the dock grows by one row and <c>dock-inset.js</c> re-measures it.
/// </summary>
public sealed partial class KaraokeBanner
{
    private ChordChartData? _chords;
    private ChordChartData? _sent;

    [Inject]
    private IInstrumentViewEngine Views { get; set; } = default!;

    /// <summary>The ready chord chart of the track, or null (no chords on the banner).</summary>
    [Parameter]
    public ChordChartData? Chords { get; set; }

    private string? ChordsFlag => _chords is not null ? "true" : null;

    private async Task SyncChordsAsync()
    {
        var wanted = EngineReady ? Chords : null;
        if (!ReferenceEquals(wanted, _sent))
        {
            // A chart the engine refused is not sent again; the flag stays off.
            _sent = wanted;
            await SendChordsAsync(wanted);
            StateHasChanged();
        }
    }

    private async Task SendChordsAsync(ChordChartData? chart)
    {
        try
        {
            await Views.SetKaraokeChordsAsync(chart);
            _chords = chart;
        }
        catch (AudioEngineException ex)
        {
            _chords = null;
            Error = $"The chords could not be shown on the lyrics: {ex.Message}";
        }
    }
}
