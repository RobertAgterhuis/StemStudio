using System.Globalization;
using StemStudio.Contracts.Scores;

namespace StemStudio.Web.Scores;

/// <summary>
/// The manual tempo of a score as typed (feature 008, FR-006, data-model 4.3): required when the beat grid is unreliable
/// or absent (prefilled from the track's detected tempo), else optional and empty (the beat grid is used).
/// </summary>
/// <param name="required">True when the grid cannot be used.</param>
/// <param name="suggested">The track's detected tempo, used to prefill a required tempo.</param>
public sealed class TempoInput(bool required, ScoreTempoDto? suggested)
{
    /// <summary>Message for a missing value of a required tempo.</summary>
    public const string MissingMessage = "Enter the tempo, the beats per bar and the first downbeat.";

    /// <summary>Message for an optional tempo that is only partly filled in.</summary>
    public const string IncompleteMessage = "Enter the tempo, the beats per bar and the first downbeat, or leave all three empty.";

    /// <summary>Message for a tempo outside 30..300.</summary>
    public const string BpmMessage = "The tempo must be between 30 and 300 BPM.";

    /// <summary>Message for beats per bar outside 1..12 or not whole.</summary>
    public const string BeatsMessage = "The beats per bar must be a whole number from 1 to 12.";

    /// <summary>Message for a first downbeat outside 0..900 s.</summary>
    public const string DownbeatMessage = "The first downbeat must be between 0 and 900 seconds.";

    /// <summary>True when the score cannot be made without a tempo.</summary>
    public bool Required { get; } = required;

    /// <summary>Tempo in BPM as typed.</summary>
    public string Bpm { get; set; } = Prefill(required, suggested?.Bpm);

    /// <summary>Beats per bar as typed.</summary>
    public string BeatsPerBar { get; set; } = Prefill(required, suggested?.BeatsPerBar);

    /// <summary>First downbeat in seconds (from the start of the source or range) as typed.</summary>
    public string FirstDownbeat { get; set; } = Prefill(required, suggested?.FirstDownbeatSeconds);

    private bool IsEmpty => string.IsNullOrWhiteSpace(Bpm) && string.IsNullOrWhiteSpace(BeatsPerBar) && string.IsNullOrWhiteSpace(FirstDownbeat);

    /// <summary>Checks the tempo.</summary>
    /// <returns>A readable problem, or null (also for an empty optional tempo).</returns>
    public string? Validate() => !Required && IsEmpty ? null : ValidateValues();

    private string? ValidateValues() =>
        TryParse(Bpm, out var bpm) && TryParse(BeatsPerBar, out var beats) && TryParse(FirstDownbeat, out var downbeat)
            ? Check(bpm, beats, downbeat)
            : Required ? MissingMessage : IncompleteMessage;

    /// <summary>The tempo to send, or null when the beat grid is used (call <see cref="Validate"/> first).</summary>
    /// <returns>The tempo.</returns>
    public ScoreTempoDto? ToDto() =>
        !Required && IsEmpty ? null : new ScoreTempoDto(Parse(Bpm), (int)Parse(BeatsPerBar), Parse(FirstDownbeat));

    private static string? Check(double bpm, double beats, double downbeat) =>
        bpm is < 30 or > 300 ? BpmMessage
        : beats is < 1 or > 12 || beats != Math.Floor(beats) ? BeatsMessage
        : downbeat is < 0 or > 900 ? DownbeatMessage
        : null;

    private static string Prefill(bool required, double? value) =>
        required && value is { } v ? v.ToString(CultureInfo.InvariantCulture) : string.Empty;

    private static bool TryParse(string text, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    private static double Parse(string text) => double.Parse(text, CultureInfo.InvariantCulture);
}
