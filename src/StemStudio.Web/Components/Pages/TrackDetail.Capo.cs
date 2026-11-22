using Microsoft.AspNetCore.Components;
using StemStudio.Contracts.InstrumentViews;
using StemStudio.Web.InstrumentViews;

namespace StemStudio.Web.Components.Pages;

/// <summary>
/// Capo mode on the track page (016 US8): offered to a player whose remembered "Practise as" family is guitar; the capo
/// drives the chord sheet and prefills the tablature capo of the sheet music dialog (FR-078).
/// </summary>
public sealed partial class TrackDetail
{
    [Inject]
    private ViewPreferences ViewPreferences { get; set; } = default!;

    /// <summary>The capo the sheet music dialog prefills (FR-078).</summary>
    internal int ScoreCapo { get; private set; }

    private bool Guitarist { get; set; }

    private async Task LoadGuitaristAsync() =>
        Guitarist = InstrumentFamilies.Parse(await ViewPreferences.GetAsync<string>(ViewPreferences.FamilyKey)) == InstrumentFamilyDto.Guitar;

    private void OnCapoChanged(CapoSetting setting) => ScoreCapo = setting.Effective;
}
