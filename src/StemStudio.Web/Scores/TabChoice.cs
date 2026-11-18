using StemStudio.Contracts;
using StemStudio.Contracts.InstrumentViews;
using StemStudio.Contracts.Scores;

namespace StemStudio.Web.Scores;

/// <summary>Layout choice of the tablature options (ui-test-ids <c>score-tab-layout</c>; <c>none</c> sends no tablature).</summary>
public enum TabLayoutChoice
{
    /// <summary>Notation and tab (default for guitar and bass, FR-020).</summary>
    NotationAndTab,

    /// <summary>Tab only.</summary>
    TabOnly,

    /// <summary>No tablature.</summary>
    None,
}

/// <summary>
/// Tablature options of the sheet music dialog (016 US2, FR-020, FR-078): layout, a catalogue tuning of the profile and
/// the capo (guitar only, prefilled from capo mode). Beginner sees the standard tuning and drop D only.
/// </summary>
/// <param name="options">The backend's tablature options for the source.</param>
/// <param name="capo">The capo of capo mode (0 when none).</param>
public sealed class TabChoice(ScoreTablatureOptionsDto options, int capo)
{
    private static readonly HashSet<TuningIdDto> BeginnerTunings =
        [TuningIdDto.GuitarStandard, TuningIdDto.GuitarDropD, TuningIdDto.Bass4Standard, TuningIdDto.Bass4DropD];

    /// <summary>The chosen layout.</summary>
    public TabLayoutChoice Layout { get; set; } = TabLayoutChoice.NotationAndTab;

    /// <summary>The chosen tuning; the backend's default at first.</summary>
    public TuningIdDto Tuning { get; set; } = options.DefaultTuning ?? options.Tunings.FirstOrDefault();

    /// <summary>True when a capo may be set (guitar).</summary>
    public bool CapoAllowed => options.CapoAllowed;

    /// <summary>The capo 0..12 (ignored unless <see cref="CapoAllowed"/>).</summary>
    public int Capo { get; set; } = options.CapoAllowed ? Math.Clamp(capo, 0, ContractLimits.CapoMax) : 0;

    /// <summary>The tunings offered at a level.</summary>
    /// <param name="all">True from Advanced on (<c>tab.tunings</c>); Beginner gets standard and drop D.</param>
    /// <returns>The tunings in the backend's order.</returns>
    public IReadOnlyList<TuningIdDto> Tunings(bool all) =>
        all ? options.Tunings : options.Tunings.Where(t => BeginnerTunings.Contains(t) || t == Tuning).ToList();

    /// <summary>The request part, or null for no tablature.</summary>
    /// <returns>The tablature.</returns>
    public ScoreTablatureDto? ToDto() => Layout == TabLayoutChoice.None
        ? null
        : new ScoreTablatureDto
        {
            Tuning = Tuning,
            Capo = CapoAllowed ? Capo : 0,
            Layout = Layout == TabLayoutChoice.TabOnly ? TabLayoutDto.TabOnly : TabLayoutDto.NotationAndTab,
        };
}
