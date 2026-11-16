using StemStudio.Contracts;
using StemStudio.Contracts.Generation;
using StemStudio.Contracts.Styles;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Mixer;
using StemStudio.Web.Services;
using StemStudio.Web.Styles;

namespace StemStudio.Web.Generation;

/// <summary>
/// The choices of the restyle dialog (009 FR-002, data-model 3.4). Defaults: the first style, the whole track up to
/// 8 minutes, every stem (vocals excluded for an instrumental style), the style's default strength and one take.
/// </summary>
public sealed class RestyleForm
{
    private readonly TrackDto _track;
    private readonly IReadOnlyList<StyleDto> _styles;
    private readonly HashSet<string> _stems;

    /// <summary>Creates the form with the defaults.</summary>
    /// <param name="track">The completed track.</param>
    /// <param name="styles">The style library, presets first (at least one style).</param>
    public RestyleForm(TrackDto track, IReadOnlyList<StyleDto> styles)
    {
        (_track, _styles) = (track, styles);
        _stems = [.. track.Stems.Select(s => s.Name)];
        End = track.DurationSeconds is { } duration ? Math.Min(duration, ContractLimits.MaxGenerationSeconds) : null;
        Style = styles[0];
        ApplyStyle();
    }

    /// <summary>The selected style.</summary>
    public StyleDto Style { get; private set; }

    /// <summary>The styles to choose from.</summary>
    public IReadOnlyList<StyleDto> Styles => _styles;

    /// <summary>Range (the range becomes the take) or in place.</summary>
    public RestyleModeDto Mode { get; set; } = RestyleModeDto.Range;

    /// <summary>Range start in seconds.</summary>
    public double Start { get; set; }

    /// <summary>Range end in seconds; null when the track length is unknown (the server default applies).</summary>
    public double? End { get; set; }

    /// <summary>Strength step 0-4 (subtle ... free).</summary>
    public int StrengthIndex { get; set; }

    /// <summary>The name of the strength step.</summary>
    public string StrengthName => StyleText.Strength((StrengthDto)StrengthIndex);

    /// <summary>Number of takes (1-2).</summary>
    public int Takes { get; set; } = 1;

    /// <summary>True when the style is instrumental: the vocals stay out of the source.</summary>
    public bool VocalsLocked => Style.VocalHandling == VocalHandlingDto.Instrumental;

    /// <summary>The included stems in track order.</summary>
    public IReadOnlyList<string> IncludedStems => [.. _track.Stems.Select(s => s.Name).Where(_stems.Contains)];

    /// <summary>The track's stems in display order.</summary>
    public IReadOnlyList<string> TrackStems => [.. _track.Stems.Select(s => s.Name)];

    /// <summary>True for the vocals of an instrumental style: they cannot be included.</summary>
    /// <param name="stem">The stem name.</param>
    /// <returns>Whether the checkbox is disabled.</returns>
    public bool IsLocked(string stem) => VocalsLocked && stem == StemNames.Vocals;

    /// <summary>The end as input text ("" when unknown).</summary>
    public string EndText => End is { } end ? InputValue.Format(end) : string.Empty;

    /// <summary>Sets the start from an input; empty or invalid means 0.</summary>
    /// <param name="value">The parsed input.</param>
    public void SetStart(double? value) => Start = value ?? 0;

    /// <summary>Sets the strength step from the slider; empty keeps the step, values are clamped to 0-4.</summary>
    /// <param name="value">The parsed input.</param>
    public void SetStrength(double? value) => StrengthIndex = (int)Math.Clamp(value ?? StrengthIndex, 0, (int)StrengthDto.Free);

    /// <summary>Sets the number of takes from an input; empty keeps the number (validation reports values outside 1-2).</summary>
    /// <param name="value">The parsed input.</param>
    public void SetTakes(double? value) => Takes = (int)(value ?? Takes);

    /// <summary>Selects a style and takes over its default strength; unknown ids are ignored.</summary>
    /// <param name="styleId">The style id.</param>
    public void SelectStyle(Guid styleId)
    {
        if (_styles.FirstOrDefault(s => s.Id == styleId) is { } style)
        {
            Style = style;
            ApplyStyle();
        }
    }

    /// <summary>Includes or excludes a stem; the vocals of an instrumental style stay excluded.</summary>
    /// <param name="stem">The stem name.</param>
    /// <param name="included">The new state.</param>
    public void SetStem(string stem, bool included)
    {
        if (included && !IsLocked(stem))
        {
            _stems.Add(stem);
        }
        else if (!included)
        {
            _stems.Remove(stem);
        }
    }

    /// <summary>Checks the choices (the API checks them again).</summary>
    /// <returns>A readable problem, or null.</returns>
    public string? Validate() => ModeProblem() ?? RangeProblem() ?? ChoiceProblem();

    private string? ModeProblem() =>
        Mode == RestyleModeDto.InPlace && _track.DurationSeconds > ContractLimits.MaxGenerationSeconds
            ? "Restyle in place works on tracks of at most 8 minutes."
            : null;

    private string? ChoiceProblem() =>
        _stems.Count == 0 ? "Choose at least one stem."
        : Takes is < 1 or > ContractLimits.MaxTakes ? "Choose 1 or 2 takes."
        : null;

    /// <summary>The request body (call <see cref="Validate"/> first).</summary>
    /// <returns>The restyle request.</returns>
    public RestyleRequest Request() => new()
    {
        StyleId = Style.Id,
        Mode = Mode,
        RangeStartSeconds = Start,
        RangeEndSeconds = End,
        IncludedStems = IncludedStems,
        Strength = (StrengthDto)StrengthIndex,
        Takes = Takes,
    };

    private void ApplyStyle()
    {
        StrengthIndex = (int)Style.DefaultStrength;
        if (VocalsLocked)
        {
            _stems.Remove(StemNames.Vocals);
        }
    }

    private string? RangeProblem()
    {
        if (End is not { } end)
        {
            return null;
        }

        return end - Start is < ContractLimits.MinGenerationSeconds or > ContractLimits.MaxGenerationSeconds
            ? "The range must be 10 seconds to 8 minutes long."
            : InsideProblem(end, _track.DurationSeconds ?? end);
    }

    private string? InsideProblem(double end, double duration) =>
        Start < 0 || end > duration ? $"The range must lie inside the track (0:00 to {TimeText.Duration(duration)})." : null;
}
