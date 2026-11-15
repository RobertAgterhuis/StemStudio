using System.Globalization;
using StemStudio.Contracts.Scores;

namespace StemStudio.Web.Scores;

/// <summary>Readable texts of sheet music (feature 008, ui-test-ids 1.7.0).</summary>
public static class ScoreText
{
    // The quality notes of the backend contract (ScoreOptions.limits); the library shows them without asking for options.
    private static readonly Dictionary<ScoreProfileDto, string> Limits = new()
    {
        [ScoreProfileDto.Vocals] = "One melody line; ornaments, slides and breath noises are simplified or missed.",
        [ScoreProfileDto.Bass] = "One note at a time; written an octave higher (bass clef 8vb); slides and ghost notes are missed.",
        [ScoreProfileDto.Guitar] = "Chords are notated as stacked notes without fingering or tablature; strums can add extra notes.",
        [ScoreProfileDto.Keys] = "Two staves split at middle C; pedal and voicing are not notated.",
        [ScoreProfileDto.Piano] = "Two staves split at middle C; the accurate model is slow on the CPU.",
        [ScoreProfileDto.Drums] = "Kick, snare, toms, hi-hat and cymbals on one staff; bleed between the detail stems can add extra hits.",
    };

    /// <summary>The wire value of a profile (<c>bass</c>), used as option value.</summary>
    /// <param name="profile">The profile.</param>
    /// <returns>The value.</returns>
    public static string Value(ScoreProfileDto profile) => profile.ToString().ToLowerInvariant();

    /// <summary>The label of a profile in the select.</summary>
    /// <param name="profile">The profile.</param>
    /// <returns>For example "Vocals (melody)".</returns>
    public static string Label(ScoreProfileDto profile) => profile == ScoreProfileDto.Vocals ? "Vocals (melody)" : profile.ToString();

    /// <summary>The quality note of a profile (<c>asset-score-limits-&lt;id&gt;</c>).</summary>
    /// <param name="profile">The profile.</param>
    /// <returns>The note.</returns>
    public static string Limit(ScoreProfileDto profile) => Limits[profile];

    /// <summary>The key of a score: "A-flat major (83 %)", or "E minor (your choice)" for an override.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The text.</returns>
    public static string Key(KeyInfoDto key) =>
        $"{KeyName($"{key.Tonic} {key.Mode.ToString().ToLowerInvariant()}")} ({Confidence(key)})";

    /// <summary>A music21 key name for people: "B- major" is "B-flat major", "F# minor" is "F-sharp minor".</summary>
    /// <param name="name">A name of <c>ContractLimits.KeyNames</c>.</param>
    /// <returns>The label.</returns>
    public static string KeyName(string name) => name.Replace("- ", "-flat ", StringComparison.Ordinal).Replace("# ", "-sharp ", StringComparison.Ordinal);

    /// <summary>Tempo and time signature: "118 BPM, 4/4".</summary>
    /// <param name="facts">The score facts.</param>
    /// <returns>The text.</returns>
    public static string Tempo(ScoreFactsDto facts) => $"{facts.Bpm.ToString(CultureInfo.InvariantCulture)} BPM, {facts.TimeSignature}";

    private static string Confidence(KeyInfoDto key) => key.Source == KeySourceDto.Override
        ? "your choice"
        : $"{Math.Round(key.Confidence * 100).ToString(CultureInfo.InvariantCulture)} %";
}
