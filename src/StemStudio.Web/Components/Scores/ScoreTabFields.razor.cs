using Microsoft.AspNetCore.Components;
using StemStudio.Contracts;
using StemStudio.Contracts.InstrumentViews;
using StemStudio.Web.Api;
using StemStudio.Web.Experience;
using StemStudio.Web.InstrumentViews;
using StemStudio.Web.Scores;
using StemStudio.Web.Services;

namespace StemStudio.Web.Components.Scores;

/// <summary>Tablature options of the sheet music dialog (016 US2); tuning names come from the instrument catalogue.</summary>
public sealed partial class ScoreTabFields
{
    /// <summary>The tablature choices.</summary>
    [Parameter, EditorRequired]
    public TabChoice Tab { get; set; } = default!;

    /// <summary>The cascaded level.</summary>
    [CascadingParameter]
    public ExperienceContext? Experience { get; set; }

    [Inject]
    private InstrumentCatalogueCache Catalogue { get; set; } = default!;

    private Dictionary<TuningIdDto, string> Labels { get; set; } = [];

    private bool AllTunings => (Experience ?? ExperienceContext.Default).IsVisible("tab.tunings");

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        try
        {
            Labels = (await Catalogue.GetAsync()).Tunings.ToDictionary(t => t.Id, t => t.Label);
        }
        catch (ApiException)
        {
            // Without the catalogue the wire names are shown.
        }
    }

    private string TuningLabel(TuningIdDto tuning) => Labels.TryGetValue(tuning, out var label) ? label : WireName.Of(tuning);

    private void SetLayout(string? value) => Tab.LayoutValue = value ?? TabChoice.NotationAndTabValue;

    private void SetTuning(string? value)
    {
        foreach (var tuning in Tab.Tunings(all: true).Where(t => WireName.Of(t) == value))
        {
            Tab.Tuning = tuning;
        }
    }

    private void SetCapo(int capo) => Tab.Capo = Math.Clamp(capo, 0, ContractLimits.CapoMax);
}
