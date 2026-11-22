namespace StemStudio.Domain.Scores;

/// <summary>Instrument profiles (data-model 2.1): suggestion per channel, allowed profiles, defaults, names and limits.</summary>
public static class ProfileCatalog
{
    /// <summary>Credit of the piano model (CC BY 4.0), shown with the piano quality choice.</summary>
    public const string PianoAttribution = "Piano transcription model: Kong et al., ByteDance (CC BY 4.0).";

    private static readonly Dictionary<ScoreProfile, (string Name, string Limit)> Facts = new()
    {
        [ScoreProfile.Vocals] = ("Vocals", "One melody line; ornaments, slides and breath noises are simplified or missed."),
        [ScoreProfile.Bass] = ("Bass", "One note at a time; written an octave higher (bass clef 8vb); slides and ghost notes are missed."),
        [ScoreProfile.Guitar] = ("Guitar", "Chords are notated as stacked notes without fingering or tablature; strums can add extra notes."),
        [ScoreProfile.Keys] = ("Keys", "Two staves split at middle C; pedal and voicing are not notated."),
        [ScoreProfile.Piano] = ("Piano", "Two staves split at middle C; the accurate model is slow on the CPU."),
        [ScoreProfile.Drums] = ("Drums", "Kick, snare, toms, hi-hat and cymbals on one staff; bleed between the detail stems can add extra hits."),
    };

    /// <summary>Every pitched profile, in order.</summary>
    public static IReadOnlyList<ScoreProfile> Pitched { get; } = [ScoreProfile.Vocals, ScoreProfile.Bass, ScoreProfile.Guitar, ScoreProfile.Keys, ScoreProfile.Piano];

    /// <summary>The suggested profile of a channel; null for single drum pieces and unknown channels.</summary>
    /// <param name="channel">The channel id, e.g. <c>vocals.lead</c>.</param>
    /// <returns>The profile or null.</returns>
    public static ScoreProfile? Suggest(string channel)
    {
        ArgumentNullException.ThrowIfNull(channel);
        var parts = channel.Split('.');
        return (parts[0], parts.Length) switch
        {
            (_, > 2) => null,
            ("vocals", _) => ScoreProfile.Vocals,
            ("bass", 1) => ScoreProfile.Bass,
            ("guitar", _) => ScoreProfile.Guitar,
            ("piano", 1) => ScoreProfile.Piano,
            ("piano" or "other", _) => ScoreProfile.Keys,
            ("drums", 1) => ScoreProfile.Drums,
            _ => null,
        };
    }

    /// <summary>The drum pieces that can be notated alone (feature 016: source <c>drums.&lt;piece&gt;</c>).</summary>
    public static IReadOnlyList<string> DrumPieces { get; } = ["kick", "snare", "toms", "hihat", "cymbals"];

    /// <summary>
    /// The profiles a source offers: drums only for the drums channel or a single drum piece, the pitched ones for a pitched
    /// channel or a library item.
    /// </summary>
    /// <param name="channel">The channel id, or null for a library item.</param>
    /// <returns>The profiles.</returns>
    public static IReadOnlyList<ScoreProfile> Allowed(string? channel) => channel is null
        ? Pitched
        : Suggest(channel) switch
        {
            null when DrumPiece(channel) is not null => [ScoreProfile.Drums],
            null => [],
            ScoreProfile.Drums => [ScoreProfile.Drums],
            _ => Pitched,
        };

    /// <summary>The drum piece of a <c>drums.&lt;piece&gt;</c> channel, else null.</summary>
    /// <param name="channel">The channel id, or null.</param>
    /// <returns>E.g. <c>kick</c>, or null.</returns>
    public static string? DrumPiece(string? channel) =>
        channel is not null && channel.StartsWith("drums.", StringComparison.Ordinal) && DrumPieces.Contains(channel[6..], StringComparer.Ordinal)
            ? channel[6..]
            : null;

    /// <summary>The default resolution: 1/8 for vocals, else 1/16.</summary>
    /// <param name="profile">The profile.</param>
    /// <returns>The resolution.</returns>
    public static ScoreResolution DefaultResolution(ScoreProfile profile) => profile == ScoreProfile.Vocals ? ScoreResolution.Eighth : ScoreResolution.Sixteenth;

    /// <summary>The part name printed on the score.</summary>
    /// <param name="profile">The profile.</param>
    /// <returns>E.g. "Bass".</returns>
    public static string InstrumentName(ScoreProfile profile) => Facts[profile].Name;

    /// <summary>What the profile cannot notate (shown before generating).</summary>
    /// <param name="profile">The profile.</param>
    /// <returns>The text.</returns>
    public static string Limit(ScoreProfile profile) => Facts[profile].Limit;

    /// <summary>The engine of a profile and quality.</summary>
    /// <param name="profile">The profile.</param>
    /// <param name="quality">The quality (piano only).</param>
    /// <returns>The engine.</returns>
    public static ScoreEngine Engine(ScoreProfile profile, ScoreQuality? quality) => profile switch
    {
        ScoreProfile.Drums => ScoreEngine.DrumOnsets,
        ScoreProfile.Piano when quality == ScoreQuality.Accurate => ScoreEngine.PianoCrnn,
        _ => ScoreEngine.BasicPitch,
    };
}

/// <summary>The 30 key signatures in music21 spelling (<c>-</c> = flat), the values of <c>keyOverride</c>.</summary>
public static class KeyNames
{
    private static readonly string[] MajorTonics = ["C", "G", "D", "A", "E", "B", "F#", "C#", "F", "B-", "E-", "A-", "D-", "G-", "C-"];

    private static readonly string[] MinorTonics = ["A", "E", "B", "F#", "C#", "G#", "D#", "A#", "D", "G", "C", "F", "B-", "E-", "A-"];

    /// <summary>Every key name, majors first.</summary>
    public static IReadOnlyList<string> All { get; } = MajorTonics.Select(tonic => tonic + " major").Concat(MinorTonics.Select(tonic => tonic + " minor")).ToList();

    /// <summary>True for one of <see cref="All"/> (case-sensitive).</summary>
    /// <param name="name">The name.</param>
    /// <returns>Whether it is a key name.</returns>
    public static bool IsKnown(string? name) => name is not null && All.Contains(name, StringComparer.Ordinal);
}
