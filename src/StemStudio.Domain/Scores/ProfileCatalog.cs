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
        [ScoreProfile.Strings] = ("Strings", "One line: a string section is reduced to its loudest line; bowing and divisi are not notated."),
        [ScoreProfile.Brass] = ("Brass", "One line: a brass section is reduced to its loudest line; falls, growls and mutes are not notated."),
        [ScoreProfile.Woodwinds] = ("Woodwinds", "One line: a woodwind section is reduced to its loudest line; trills and breath noises are simplified."),
    };

    private static readonly Dictionary<string, ScoreProfile> StemProfiles = new(StringComparer.Ordinal)
    {
        ["vocals"] = ScoreProfile.Vocals,
        ["bass"] = ScoreProfile.Bass,
        ["guitar"] = ScoreProfile.Guitar,
        ["piano"] = ScoreProfile.Piano,
        ["other"] = ScoreProfile.Keys,
        ["drums"] = ScoreProfile.Drums,
    };

    private static readonly Dictionary<string, ScoreProfile> DetailParentProfiles = new(StringComparer.Ordinal)
    {
        ["vocals"] = ScoreProfile.Vocals,
        ["guitar"] = ScoreProfile.Guitar,
        ["piano"] = ScoreProfile.Keys,
        ["other"] = ScoreProfile.Keys,
    };

    private static readonly Dictionary<string, ScoreProfile> SectionProfiles = new(StringComparer.Ordinal)
    {
        ["strings"] = ScoreProfile.Strings,
        ["brass"] = ScoreProfile.Brass,
        ["woodwinds"] = ScoreProfile.Woodwinds,
    };

    /// <summary>The 008 pitched profiles, in order, offered for every pitched source (a section profile is added for its own stem).</summary>
    public static IReadOnlyList<ScoreProfile> Pitched { get; } = [ScoreProfile.Vocals, ScoreProfile.Bass, ScoreProfile.Guitar, ScoreProfile.Keys, ScoreProfile.Piano];

    /// <summary>
    /// The suggested profile of a channel: the section profile for <c>&lt;parent&gt;.strings|brass|woodwinds</c> (feature 016), else
    /// the 008 rule; null for single drum pieces and unknown channels.
    /// </summary>
    /// <param name="channel">The channel id, e.g. <c>vocals.lead</c>.</param>
    /// <returns>The profile or null.</returns>
    public static ScoreProfile? Suggest(string channel)
    {
        ArgumentNullException.ThrowIfNull(channel);
        return channel.Split('.') switch
        {
            [_, var detail] when SectionProfiles.TryGetValue(detail, out var section) => section,
            [var stem] => Lookup(StemProfiles, stem),
            [var parent, _] => Lookup(DetailParentProfiles, parent),
            _ => null,
        };
    }

    /// <summary>True for <c>strings</c>, <c>brass</c> and <c>woodwinds</c> (single-line section profiles, feature 016).</summary>
    /// <param name="profile">The profile.</param>
    /// <returns>Whether it is a section profile.</returns>
    public static bool IsSection(ScoreProfile profile) => SectionProfiles.ContainsValue(profile);

    /// <summary>The drum pieces that can be notated alone (feature 016: source <c>drums.&lt;piece&gt;</c>).</summary>
    public static IReadOnlyList<string> DrumPieces { get; } = ["kick", "snare", "toms", "hihat", "cymbals"];

    /// <summary>
    /// The profiles a source offers: drums only for the drums channel or a single drum piece, the 008 pitched ones for a pitched
    /// channel or a library item, plus the section profile when it is the suggestion (at most six, the contract limit).
    /// </summary>
    /// <param name="channel">The channel id, or null for a library item.</param>
    /// <param name="suggestion">The suggestion of a library item (ignored with a channel).</param>
    /// <returns>The profiles.</returns>
    public static IReadOnlyList<ScoreProfile> Allowed(string? channel, ScoreProfile? suggestion = null)
    {
        var suggested = channel is null ? suggestion : Suggest(channel) ?? (DrumPiece(channel) is null ? null : ScoreProfile.Drums);
        return suggested switch
        {
            ScoreProfile.Drums => [ScoreProfile.Drums],
            { } profile when IsSection(profile) => [.. Pitched, profile],
            null when channel is not null => [],
            _ => Pitched,
        };
    }

    /// <summary>The drum piece of a <c>drums.&lt;piece&gt;</c> channel, else null.</summary>
    /// <param name="channel">The channel id, or null.</param>
    /// <returns>E.g. <c>kick</c>, or null.</returns>
    public static string? DrumPiece(string? channel) =>
        channel is not null && channel.StartsWith("drums.", StringComparison.Ordinal) && DrumPieces.Contains(channel[6..], StringComparer.Ordinal)
            ? channel[6..]
            : null;

    private static ScoreProfile? Lookup(Dictionary<string, ScoreProfile> profiles, string name) => profiles.TryGetValue(name, out var profile) ? profile : null;

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
