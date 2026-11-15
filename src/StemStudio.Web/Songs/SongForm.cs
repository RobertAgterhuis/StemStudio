using System.Globalization;
using StemStudio.Contracts;
using StemStudio.Contracts.Generation;
using StemStudio.Contracts.Lyrics;
using StemStudio.Contracts.Styles;
using StemStudio.Web.Api;

namespace StemStudio.Web.Songs;

/// <summary>
/// The choices of the song page (010 FR-001, FR-004, data-model 3.2): lyrics (typed, uploaded or a saved sheet), title,
/// language, a library style or a free description, lead voice and delivery, instrumental, tempo, key, beats per bar,
/// length (10-480 s, default 120) and takes (1-2). A saved sheet whose text is unchanged is sent by id.
/// </summary>
public sealed class SongForm
{
    /// <summary>Lyrics density above which the page warns (words per second).</summary>
    public const double DensityLimit = ContractLimits.LyricsDensityWarningWordsPerSecond;

    private readonly List<LyricsSheetDto> _sheets;

    /// <summary>Creates the form with the defaults.</summary>
    /// <param name="styles">The style library (the first one is preselected).</param>
    /// <param name="sheets">The saved lyrics sheets.</param>
    public SongForm(IReadOnlyList<StyleDto> styles, IEnumerable<LyricsSheetDto> sheets)
    {
        Styles = styles;
        StyleId = styles.Count > 0 ? styles[0].Id : null;
        _sheets = [.. sheets];
    }

    /// <summary>The style library.</summary>
    public IReadOnlyList<StyleDto> Styles { get; }

    /// <summary>The saved sheets.</summary>
    public IReadOnlyList<LyricsSheetDto> Sheets => _sheets;

    /// <summary>The selected saved sheet; null for new lyrics.</summary>
    public LyricsSheetDto? Sheet { get; private set; }

    /// <summary>The lyrics as typed.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Song title ("" = the API default).</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Language code.</summary>
    public string Language { get; set; } = "en";

    /// <summary>The library style; null for a free description.</summary>
    public Guid? StyleId { get; set; }

    /// <summary>Free description (used without a style).</summary>
    public string FreeCaption { get; set; } = string.Empty;

    /// <summary>Lead voice.</summary>
    public LeadVoiceDto LeadVoice { get; set; } = LeadVoiceDto.Any;

    /// <summary>Delivery words, e.g. "breathy, soft".</summary>
    public string Delivery { get; set; } = string.Empty;

    /// <summary>No vocals.</summary>
    public bool Instrumental { get; set; }

    /// <summary>Tempo; null = automatic.</summary>
    public double? Bpm { get; set; }

    /// <summary>Key such as "E minor"; "" = automatic.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Beats per bar; "" = automatic.</summary>
    public string TimeSignature { get; set; } = string.Empty;

    /// <summary>Length in seconds.</summary>
    public double Duration { get; set; } = 120;

    /// <summary>Number of takes.</summary>
    public int Takes { get; set; } = 1;

    /// <summary>The counter text, e.g. "122 / 4096".</summary>
    public string CountText => string.Create(CultureInfo.InvariantCulture, $"{Text.Length} / {ContractLimits.LyricsSheetMaxLength}");

    /// <summary>The warning when the lyrics are too dense for the length, else null (generating stays possible).</summary>
    public string? DensityWarning
    {
        get
        {
            var density = SongText.Words(Text) / Math.Max(Duration, ContractLimits.MinGenerationSeconds);
            return density > DensityLimit
                ? string.Create(CultureInfo.InvariantCulture, $"About {density:0.0} words per second: the singing may sound rushed. Make the song longer or the lyrics shorter.")
                : null;
        }
    }

    /// <summary>Sets the length from the slider; empty keeps it, values are clamped to 10-480 s.</summary>
    /// <param name="seconds">The parsed input.</param>
    public void SetDuration(double? seconds) =>
        Duration = Math.Clamp(seconds ?? Duration, ContractLimits.MinGenerationSeconds, ContractLimits.MaxGenerationSeconds);

    /// <summary>Sets the tempo from its input; empty means automatic.</summary>
    /// <param name="bpm">The parsed input.</param>
    public void SetBpm(double? bpm) => Bpm = bpm;

    /// <summary>Sets the number of takes; empty keeps it.</summary>
    /// <param name="takes">The parsed input.</param>
    public void SetTakes(double? takes) => Takes = (int)(takes ?? Takes);

    /// <summary>Selects a saved sheet (its text, title and language), or new lyrics for an unknown id or null.</summary>
    /// <param name="sheetId">The sheet id.</param>
    public void SelectSheet(Guid? sheetId)
    {
        Sheet = _sheets.FirstOrDefault(s => s.Id == sheetId);
        if (Sheet is { } sheet)
        {
            (Text, Title, Language) = (sheet.Text, sheet.Title, sheet.Language);
        }
    }

    /// <summary>Adds or replaces a sheet (saved, uploaded or loaded by id) and selects it.</summary>
    /// <param name="sheet">The sheet.</param>
    public void UseSheet(LyricsSheetDto sheet)
    {
        _sheets.RemoveAll(s => s.Id == sheet.Id);
        _sheets.Insert(0, sheet);
        SelectSheet(sheet.Id);
    }

    /// <summary>Checks the lyrics and the tempo (the API checks everything again).</summary>
    /// <returns>The problem, or null.</returns>
    public FieldProblem? Validate() =>
        Text.Trim().Length == 0 ? new FieldProblem("lyricsText", "Enter the lyrics first.")
        : Text.Length > ContractLimits.LyricsSheetMaxLength ? new FieldProblem("lyricsText", "The lyrics can be at most 4096 characters.")
        : Bpm is < ContractLimits.MinSongBpm or > ContractLimits.MaxSongBpm ? new FieldProblem("bpm", "The tempo must be 40 to 220 BPM.")
        : Takes is < 1 or > ContractLimits.MaxTakes ? new FieldProblem("takes", "Choose 1 or 2 takes.")
        : null;

    /// <summary>The lyrics as a sheet input (title "Untitled lyrics" when empty).</summary>
    /// <returns>The input.</returns>
    public LyricsInput LyricsInput() => new() { Title = OrNull(Title) ?? "Untitled lyrics", Language = Language, Text = Text };

    /// <summary>The request body (call <see cref="Validate"/> first).</summary>
    /// <returns>The song request.</returns>
    public SongRequest Request() => new()
    {
        LyricsId = SendsSheet ? Sheet!.Id : null,
        LyricsText = SendsSheet ? null : Text,
        Title = OrNull(Title),
        Language = Language,
        StyleId = StyleId,
        FreeCaption = StyleId is null ? OrNull(FreeCaption) : null,
        LeadVoice = LeadVoice,
        Delivery = OrNull(Delivery),
        Instrumental = Instrumental,
        Bpm = (int?)Bpm,
        KeyScale = OrNull(Key),
        TimeSignature = OrNull(TimeSignature),
        DurationSeconds = Duration,
        Takes = Takes,
    };

    private bool SendsSheet => Sheet is not null && Sheet.Text == Text;

    private static string? OrNull(string text) => text.Trim() is { Length: > 0 } trimmed ? trimmed : null;
}
