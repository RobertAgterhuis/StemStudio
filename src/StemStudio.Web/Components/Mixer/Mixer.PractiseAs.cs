using StemStudio.Contracts.InstrumentViews;
using StemStudio.Contracts.Scores;
using StemStudio.Web.InstrumentViews;

namespace StemStudio.Web.Components.Mixer;

/// <summary>
/// "Practise as ..." of the mixer page (016 US6, FR-060): the family's minus-one mix (data-model 5.3) and its view; the choice
/// is remembered in the browser and offered again on the next visit, never applied silently (AS3).
/// </summary>
public sealed partial class Mixer
{
    /// <summary>The family practised in this visit, or null.</summary>
    private InstrumentFamilyDto? Family { get; set; }

    /// <summary>The remembered family offered again, or null.</summary>
    private InstrumentFamilyDto? OfferedFamily { get; set; }

    /// <summary>The written instrument of the orchestral view (concert until one is chosen).</summary>
    private WrittenInstrumentDto WrittenInstrument { get; set; } = WrittenInstrumentDto.Concert;

    // The written key applies to the orchestral family with a transposing (non-concert) instrument (FR-074).
    private WrittenInstrumentDto? PartInstrument =>
        Family == InstrumentFamilyDto.Orchestral && WrittenInstrument != WrittenInstrumentDto.Concert ? WrittenInstrument : null;

    private string ViewLabel => OpenedView switch
    {
        InstrumentViewDto.DrumTrainer => "Drum trainer",
        InstrumentViewDto.Singing => "Singing feedback",
        InstrumentViewDto.Chords => "Chords",
        InstrumentViewDto.Tablature => "Tablature",
        _ => "Part for your instrument",
    };

    /// <summary>The channel a family's sheet music is made from: its first channel on this track.</summary>
    private string FamilyChannel => State!.AllChannels.FirstOrDefault(c => Family is { } f && InstrumentFamilies.Contains(State, c, f))?.Stem ?? "other";

    // The profile the family's score view prefers (the orchestral one follows the split detail stem).
    private ScoreProfileDto FamilyProfile => Family switch
    {
        InstrumentFamilyDto.Bass => ScoreProfileDto.Bass,
        InstrumentFamilyDto.Orchestral => FamilyChannel.EndsWith("brass", StringComparison.Ordinal) ? ScoreProfileDto.Brass
            : FamilyChannel.EndsWith("woodwinds", StringComparison.Ordinal) ? ScoreProfileDto.Woodwinds : ScoreProfileDto.Strings,
        _ => ScoreProfileDto.Guitar,
    };

    private async Task LoadOfferAsync() =>
        OfferedFamily = InstrumentFamilies.Parse(await ViewPreferences.GetAsync<string>(ViewPreferences.FamilyKey));

    private async Task PractiseAsAsync(InstrumentFamilyDto family)
    {
        (Family, OfferedFamily) = (family, null);
        foreach (var channel in FamilyMixService.MinusOne(State!, family))
        {
            await SendChannelAsync(channel);
        }

        await ViewPreferences.SetAsync(ViewPreferences.FamilyKey, InstrumentFamilies.Id(family));
        OpenedView = InstrumentFamilies.View(family);
    }

    private Task AcceptOfferAsync() => OfferedFamily is { } family ? PractiseAsAsync(family) : Task.CompletedTask;

    private void OnWrittenInstrument(WrittenInstrumentDto instrument) => WrittenInstrument = instrument;
}
