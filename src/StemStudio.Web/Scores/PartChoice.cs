using StemStudio.Contracts.InstrumentViews;
using StemStudio.Contracts.Scores;

namespace StemStudio.Web.Scores;

/// <summary>
/// The written instrument and clef of the sheet music dialog (016 US5, FR-051, FR-052): concert pitch and the automatic
/// clef send no part, so the result stays the 008 score.
/// </summary>
/// <param name="options">The backend's part options for the source.</param>
public sealed class PartChoice(ScorePartOptionsDto options)
{
    /// <summary>The chosen instrument; the backend's default at first.</summary>
    public WrittenInstrumentDto Instrument { get; set; } = options.DefaultInstrument;

    /// <summary>The chosen clef.</summary>
    public ClefChoiceDto Clef { get; set; } = ClefChoiceDto.Auto;

    /// <summary>True when the backend lets the source choose a clef.</summary>
    public bool ClefAllowed => options.ClefAllowed;

    /// <summary>True when a profile can take a clef choice (not the grand-staff profiles, data-model 4.3).</summary>
    /// <param name="profile">The profile.</param>
    /// <returns>Whether the clef select is shown.</returns>
    public bool ClefFor(ScoreProfileDto? profile) => ClefAllowed && profile is not (ScoreProfileDto.Keys or ScoreProfileDto.Piano);

    /// <summary>The request part.</summary>
    /// <param name="withClef">True when the clef applies.</param>
    /// <returns>The part, or null for concert pitch with the automatic clef.</returns>
    public ScorePartDto? ToDto(bool withClef)
    {
        var clef = withClef ? Clef : ClefChoiceDto.Auto;
        return Instrument == WrittenInstrumentDto.Concert && clef == ClefChoiceDto.Auto ? null : new ScorePartDto { Instrument = Instrument, Clef = clef };
    }
}
