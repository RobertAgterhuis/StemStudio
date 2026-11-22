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
    /// <summary>Select value of <see cref="TabLayoutChoice.NotationAndTab"/>.</summary>
    public const string NotationAndTabValue = "notation-and-tab";

    /// <summary>Select value of <see cref="TabLayoutChoice.TabOnly"/>.</summary>
    public const string TabOnlyValue = "tab-only";

    /// <summary>Select value of <see cref="TabLayoutChoice.None"/>.</summary>
    public const string NoneValue = "none";

    private static readonly HashSet<TuningIdDto> BeginnerTunings =
        [TuningIdDto.GuitarStandard, TuningIdDto.GuitarDropD, TuningIdDto.Bass4Standard, TuningIdDto.Bass4DropD];

    private ScoreProfileDto _profile = ScoreProfileDto.Guitar;

    /// <summary>The chosen layout.</summary>
    public TabLayoutChoice Layout { get; set; } = TabLayoutChoice.NotationAndTab;

    /// <summary>The layout as its select value (<c>notation-and-tab</c>, <c>tab-only</c>, <c>none</c>).</summary>
    public string LayoutValue
    {
        get => Layout switch
        {
            TabLayoutChoice.TabOnly => TabOnlyValue,
            TabLayoutChoice.None => NoneValue,
            _ => NotationAndTabValue,
        };
        set => Layout = value switch
        {
            TabOnlyValue => TabLayoutChoice.TabOnly,
            NoneValue => TabLayoutChoice.None,
            _ => TabLayoutChoice.NotationAndTab,
        };
    }

    /// <summary>The chosen tuning; the backend's default at first.</summary>
    public TuningIdDto Tuning { get; set; } = options.DefaultTuning ?? (options.Tunings.Count > 0 ? options.Tunings[0] : default);

    /// <summary>True when a capo may be set (guitar profile and the backend allows it).</summary>
    public bool CapoAllowed => options.CapoAllowed && _profile == ScoreProfileDto.Guitar;

    /// <summary>The capo 0..12 (ignored unless <see cref="CapoAllowed"/>).</summary>
    public int Capo { get; set; } = Math.Clamp(capo, 0, ContractLimits.CapoMax);

    /// <summary>Follows the dialog's profile: keeps the tuning when it belongs to the profile, else takes its first one.</summary>
    /// <param name="profile">The chosen profile (guitar or bass).</param>
    public void UseProfile(ScoreProfileDto profile)
    {
        _profile = profile;
        if (!Fits(Tuning, profile))
        {
            Tuning = options.Tunings.FirstOrDefault(t => Fits(t, profile));
        }
    }

    /// <summary>The tunings offered for the current profile at a level.</summary>
    /// <param name="all">True from Advanced on (<c>tab.tunings</c>); Beginner gets standard and drop D.</param>
    /// <returns>The tunings in the backend's order.</returns>
    public IReadOnlyList<TuningIdDto> Tunings(bool all) =>
        [.. options.Tunings.Where(t => Fits(t, _profile) && (all || BeginnerTunings.Contains(t) || t == Tuning))];

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

    private static bool Fits(TuningIdDto tuning, ScoreProfileDto profile) =>
        tuning.ToString().StartsWith(profile == ScoreProfileDto.Bass ? "Bass" : "Guitar", StringComparison.Ordinal);
}
