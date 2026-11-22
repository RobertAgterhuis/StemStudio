using Microsoft.AspNetCore.Components;
using StemStudio.Contracts.InstrumentViews;
using StemStudio.Contracts.Scores;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.InstrumentViews;
using StemStudio.Web.Scores;

namespace StemStudio.Web.Components.InstrumentViews;

/// <summary>The tab or part view of a family: sheet music with tablature (guitar, bass) or written for the chosen instrument.</summary>
public sealed partial class FamilyView
{
    private static readonly ScoreProfileDto[] PartProfiles = [ScoreProfileDto.Strings, ScoreProfileDto.Brass, ScoreProfileDto.Woodwinds];

    private readonly PartChoice _part = new(new ScorePartOptionsDto { Available = true, DefaultInstrument = WrittenInstrumentDto.Concert, ClefAllowed = false });

    [Inject]
    private ViewPreferences Preferences { get; set; } = default!;

    /// <summary>The family (guitar, bass or orchestral).</summary>
    [Parameter, EditorRequired]
    public InstrumentFamilyDto Family { get; set; }

    /// <summary>The track.</summary>
    [Parameter, EditorRequired]
    public TrackDto Track { get; set; } = default!;

    /// <summary>The family's channel new sheet music is made from.</summary>
    [Parameter, EditorRequired]
    public string Channel { get; set; } = string.Empty;

    /// <summary>The 014 score view, shown when matching sheet music exists.</summary>
    [Parameter]
    public RenderFragment? ScoreView { get; set; }

    /// <summary>Raised with the remembered or chosen written instrument (the written key of US7).</summary>
    [Parameter]
    public EventCallback<WrittenInstrumentDto> OnInstrument { get; set; }

    private bool IsPart => Family == InstrumentFamilyDto.Orchestral;

    private PartChoice Part => _part;

    private IReadOnlyList<ScoreProfileDto> Profiles => IsPart ? PartProfiles : [Family == InstrumentFamilyDto.Bass ? ScoreProfileDto.Bass : ScoreProfileDto.Guitar];

    private string Noun => IsPart ? "part for your instrument" : "tab";

    private string GenerateText => IsPart ? "Generate part" : "Generate tab";

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        if (IsPart && await Preferences.GetAsync<WrittenInstrumentDto?>(ViewPreferences.InstrumentKey) is { } stored && Enum.IsDefined(stored))
        {
            _part.Instrument = stored;
            await OnInstrument.InvokeAsync(stored);
        }
    }

    // Tab keeps the backend's tablature default; a part uses the chosen instrument.
    private void Configure(ScoreForm form)
    {
        if (IsPart && form.Part is { } part)
        {
            part.Instrument = _part.Instrument;
        }
    }

    private async Task RememberInstrumentAsync(WrittenInstrumentDto instrument)
    {
        await Preferences.SetAsync(ViewPreferences.InstrumentKey, instrument);
        await OnInstrument.InvokeAsync(instrument);
    }
}
