using Microsoft.AspNetCore.Components;
using StemStudio.Contracts.InstrumentViews;
using StemStudio.Web.Api;
using StemStudio.Web.InstrumentViews;
using StemStudio.Web.Scores;
using StemStudio.Web.Services;

namespace StemStudio.Web.Components.Scores;

/// <summary>Written instrument and clef of the sheet music dialog (016 US5); the choices come from the instrument catalogue.</summary>
public sealed partial class ScorePartFields
{
    /// <summary>Label of the concert pitch entry.</summary>
    public const string ConcertLabel = "Concert pitch (no transposition)";

    [Inject]
    private InstrumentCatalogueCache Catalogue { get; set; } = default!;

    /// <summary>The part choices.</summary>
    [Parameter, EditorRequired]
    public PartChoice Part { get; set; } = default!;

    /// <summary>Raised after the instrument changed (the orchestral view remembers it).</summary>
    [Parameter]
    public EventCallback<WrittenInstrumentDto> OnInstrumentChanged { get; set; }

    /// <summary>True when the clef choice applies to the profile.</summary>
    [Parameter]
    public bool ShowsClef { get; set; }

    private IReadOnlyList<WrittenInstrumentInfoDto> Instruments { get; set; } = [];

    private IReadOnlyList<ClefChoiceDto> Clefs { get; set; } = [ClefChoiceDto.Auto];

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        try
        {
            var catalogue = await Catalogue.GetAsync();
            Instruments = [.. catalogue.Instruments.OrderBy(i => i.Id != WrittenInstrumentDto.Concert)];
            Clefs = catalogue.Clefs;
        }
        catch (ApiException)
        {
            Instruments = [new WrittenInstrumentInfoDto { Id = WrittenInstrumentDto.Concert, Name = string.Empty, KeyName = "C", Interval = "P1", DefaultClef = "auto", LowMidi = 0, HighMidi = 127 }];
        }
    }

    private static string InstrumentLabel(WrittenInstrumentInfoDto instrument) =>
        instrument.Id == WrittenInstrumentDto.Concert ? ConcertLabel : instrument.Name;

    private async Task SetInstrument(string? value)
    {
        foreach (var instrument in Instruments.Where(i => WireName.Of(i.Id) == value))
        {
            Part.Instrument = instrument.Id;
            await OnInstrumentChanged.InvokeAsync(instrument.Id);
        }
    }

    private void SetClef(string? value)
    {
        foreach (var clef in Clefs.Where(c => WireName.Of(c) == value))
        {
            Part.Clef = clef;
        }
    }
}
