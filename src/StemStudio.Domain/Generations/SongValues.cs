using System.Text.RegularExpressions;
using StemStudio.Domain.Lyrics;
using StemStudio.Domain.Styles;

namespace StemStudio.Domain.Generations;

/// <summary>The lead voice of a song.</summary>
public enum LeadVoice
{
    /// <summary>Female.</summary>
    Female,

    /// <summary>Male.</summary>
    Male,

    /// <summary>Either.</summary>
    Any,
}

/// <summary>AI voice label of a take (010 FR-011).</summary>
public enum VocalLabel
{
    /// <summary>No vocals (instrumental).</summary>
    None,

    /// <summary>A synthetic AI voice.</summary>
    Synthetic,

    /// <summary>Converted to an own-voice profile.</summary>
    Converted,
}

/// <summary>What a song was made from (snapshots kept when the sheet or style changes).</summary>
public sealed record SongDetails
{
    /// <summary>The lyrics.</summary>
    public required LyricsSnapshot Lyrics { get; init; }

    /// <summary>The style, if one was chosen.</summary>
    public StyleSnapshot? Style { get; init; }

    /// <summary>The free caption, if given instead of a style.</summary>
    public string? FreeCaption { get; init; }

    /// <summary>Lead voice.</summary>
    public required LeadVoice LeadVoice { get; init; }

    /// <summary>Delivery words (at most 40).</summary>
    public string? Delivery { get; init; }

    /// <summary>True for an instrumental.</summary>
    public bool Instrumental { get; init; }

    /// <summary>Tempo.</summary>
    public int? Bpm { get; init; }

    /// <summary>Key and scale, e.g. <c>D minor</c>.</summary>
    public string? KeyScale { get; init; }

    /// <summary>Beats per bar: 2, 3, 4 or 6.</summary>
    public string? TimeSignature { get; init; }

    /// <summary>Song length.</summary>
    public required double DurationSeconds { get; init; }

    /// <summary>The caption sent to the model.</summary>
    public required string Caption { get; init; }
}

/// <summary>What a voice conversion converts (010 data-model 3.2).</summary>
public sealed record VoiceConversionDetails
{
    /// <summary>The song generation.</summary>
    public required Guid SourceGenerationId { get; init; }

    /// <summary>The song take.</summary>
    public required int SourceTakeIndex { get; init; }

    /// <summary>The profile; null after it was deleted.</summary>
    public Guid? VoiceProfileId { get; init; }

    /// <summary>Profile name snapshot.</summary>
    public required string VoiceProfileName { get; init; }

    /// <summary>The track the take was imported as (its separation gives the stems).</summary>
    public Guid? ImportedTrackId { get; init; }

    /// <summary>Pitch shift in semitones (-12..12).</summary>
    public int PitchShift { get; init; }

    /// <summary>Shift to the profile's register automatically.</summary>
    public bool AutoShift { get; init; } = true;
}

/// <summary>Inputs of a song caption.</summary>
/// <param name="Style">The style, if any.</param>
/// <param name="FreeCaption">The free caption, if any.</param>
/// <param name="LeadVoice">The lead voice.</param>
/// <param name="Delivery">Delivery words.</param>
/// <param name="Instrumental">True for an instrumental.</param>
public sealed record SongCaptionInput(StyleSnapshot? Style, string? FreeCaption, LeadVoice LeadVoice, string? Delivery, bool Instrumental);

/// <summary>The song caption: the 009 style caption (or the free caption, or "pop") plus the lead voice and delivery, at most 512 characters.</summary>
public static class SongCaptionBuilder
{
    /// <summary>Builds the caption.</summary>
    /// <param name="input">The inputs.</param>
    /// <returns>The caption.</returns>
    public static string Build(SongCaptionInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var parts = new List<string> { Base(input) };
        parts.AddRange(input.Instrumental ? Instrumental(parts[0]) : Voice(input));
        return StylePromptBuilder.TrimToWords(string.Join(", ", parts.Where(part => part.Length > 0)), StylePromptBuilder.MaxCaptionLength);
    }

    /// <summary>Imitation-guard errors of the song texts.</summary>
    /// <param name="guard">The guard.</param>
    /// <param name="freeCaption">The free caption.</param>
    /// <param name="delivery">The delivery.</param>
    /// <returns>One error per offending field.</returns>
    public static IReadOnlyList<DomainError> Check(StyleTextGuard guard, string? freeCaption, string? delivery)
    {
        ArgumentNullException.ThrowIfNull(guard);
        var errors = new ValidationErrors();
        errors.Check(!guard.IsImitation(freeCaption), "freeCaption", StyleTextGuard.Message);
        errors.Check(!guard.IsImitation(delivery), "delivery", StyleTextGuard.Message);
        return errors.Errors;
    }

    private static string Base(SongCaptionInput input) =>
        input.Style is { } style ? StylePromptBuilder.Caption(style) : input.FreeCaption?.Trim() is { Length: > 0 } free ? free : "pop";

    private static IEnumerable<string> Instrumental(string head) =>
        head.Contains("instrumental", StringComparison.OrdinalIgnoreCase) ? [] : ["instrumental"];

    private static IEnumerable<string> Voice(SongCaptionInput input) =>
        [input.LeadVoice switch { LeadVoice.Female => "female lead vocal", LeadVoice.Male => "male lead vocal", _ => "lead vocal" }, input.Delivery?.Trim() ?? string.Empty];
}

/// <summary>Song options to validate.</summary>
/// <param name="Bpm">Tempo.</param>
/// <param name="KeyScale">Key and scale.</param>
/// <param name="TimeSignature">Beats per bar.</param>
/// <param name="DurationSeconds">Length.</param>
/// <param name="Takes">Takes.</param>
/// <param name="Delivery">Delivery.</param>
/// <param name="FreeCaption">Free caption.</param>
public sealed record SongOptions(int? Bpm, string? KeyScale, string? TimeSignature, double DurationSeconds, int Takes, string? Delivery, string? FreeCaption);

/// <summary>Song option rules (backend 1.9.0 <c>SongRequest</c>).</summary>
public static partial class SongRules
{
    private static readonly string[] TimeSignatures = ["2", "3", "4", "6"];

    /// <summary>The errors of the options.</summary>
    /// <param name="options">The options.</param>
    /// <returns>Field errors.</returns>
    public static IReadOnlyList<DomainError> Validate(SongOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var errors = new ValidationErrors();
        errors.Check(options.Bpm is null or (>= 40 and <= 220), "bpm", "The tempo must be 40 to 220 BPM.");
        errors.Check(options.KeyScale is null || KeyPattern().IsMatch(options.KeyScale), "keyScale", "Use a key such as \"D minor\" or \"F# major\".");
        errors.Check(options.TimeSignature is null || TimeSignatures.Contains(options.TimeSignature), "timeSignature", "Use 2, 3, 4 or 6 beats per bar.");
        errors.Check(options.DurationSeconds is >= 10 and <= 480, "durationSeconds", "The song must be 10 to 480 seconds long.");
        errors.Check(options.Takes is >= 1 and <= Generation.MaxTakes, "takes", "Choose 1 or 2 takes.");
        errors.Check((options.Delivery?.Length ?? 0) <= 40, "delivery", "The delivery must be at most 40 characters.");
        errors.Check((options.FreeCaption?.Length ?? 0) <= 300, "freeCaption", "The caption must be at most 300 characters.");
        return errors.Errors;
    }

    [GeneratedRegex(@"\A[A-G](#|b)? (major|minor)\z", RegexOptions.CultureInvariant)]
    private static partial Regex KeyPattern();
}
