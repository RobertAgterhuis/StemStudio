using StemStudio.Domain.Rendering;

namespace StemStudio.Domain.Scores;

/// <summary>Instrument profile of a score (feature 008, data-model 2.1).</summary>
public enum ScoreProfile
{
    /// <summary>One melody line, treble clef.</summary>
    Vocals,

    /// <summary>One note at a time, bass clef 8vb.</summary>
    Bass,

    /// <summary>Treble clef 8vb, chords as stacked notes.</summary>
    Guitar,

    /// <summary>Grand staff (basic-pitch).</summary>
    Keys,

    /// <summary>Grand staff (piano model or basic-pitch).</summary>
    Piano,

    /// <summary>Percussion staff from the five drum detail stems.</summary>
    Drums,

    /// <summary>Strings section line (feature 016).</summary>
    Strings,

    /// <summary>Brass section line (feature 016).</summary>
    Brass,

    /// <summary>Woodwinds section line (feature 016).</summary>
    Woodwinds,
}

/// <summary>Piano transcription quality.</summary>
public enum ScoreQuality
{
    /// <summary>The piano CRNN (GPU when available).</summary>
    Accurate,

    /// <summary>basic-pitch.</summary>
    Fast,
}

/// <summary>Smallest note value of the quantisation grid.</summary>
public enum ScoreResolution
{
    /// <summary>1/16.</summary>
    Sixteenth,

    /// <summary>1/8 ("simplified").</summary>
    Eighth,
}

/// <summary>The transcription engine of a score.</summary>
public enum ScoreEngine
{
    /// <summary>basic-pitch (ONNX, CPU).</summary>
    BasicPitch,

    /// <summary>The piano CRNN.</summary>
    PianoCrnn,

    /// <summary>Drum onset detection.</summary>
    DrumOnsets,

    /// <summary>The fake transcriber.</summary>
    Fake,
}

/// <summary>Whether a source has a usable beat grid (data-model 4.5).</summary>
public enum GridStatus
{
    /// <summary>A reliable beat grid.</summary>
    Reliable,

    /// <summary>A beat grid the analysis does not trust.</summary>
    Unreliable,

    /// <summary>No beat grid.</summary>
    None,
}

/// <summary>Major or minor.</summary>
public enum KeyMode
{
    /// <summary>Major.</summary>
    Major,

    /// <summary>Minor.</summary>
    Minor,
}

/// <summary>Where the key of a score comes from.</summary>
public enum KeySource
{
    /// <summary>Estimated from the notes.</summary>
    Estimated,

    /// <summary>Chosen by the user.</summary>
    Override,
}

/// <summary>Warnings stored on a score (data-model 2.7).</summary>
public enum ScoreWarningCode
{
    /// <summary>Tempo and bars come from the entered values.</summary>
    ManualTempo,

    /// <summary>The key is uncertain.</summary>
    LowKeyConfidence,

    /// <summary>Notes were removed by the cleaning.</summary>
    NotesCleaned,

    /// <summary>Notes were left out in busy bars.</summary>
    DenseCapped,

    /// <summary>The source is a detail stem.</summary>
    DetailBleed,

    /// <summary>The accurate piano model ran on the CPU.</summary>
    CpuPiano,

    /// <summary>Very few notes were detected.</summary>
    FewNotes,

    /// <summary>A drum piece stem was missing.</summary>
    MissingDrumPiece,
}

/// <summary>Why a score cannot be made from a source (the API answers 409).</summary>
public enum ScoreConflict
{
    /// <summary>The drums profile needs the drum detail stems.</summary>
    DrumsNotSplit,
}

/// <summary>A score request conflicts with the state of its source.</summary>
public sealed class ScoreConflictException : InvalidOperationException
{
    /// <summary>Creates the exception for a conflict.</summary>
    /// <param name="conflict">The conflict.</param>
    public ScoreConflictException(ScoreConflict conflict)
        : base($"The score request conflicts with its source: {conflict}.")
    {
        Conflict = conflict;
    }

    /// <summary>Creates a <see cref="ScoreConflict.DrumsNotSplit"/> exception.</summary>
    public ScoreConflictException()
        : this(ScoreConflict.DrumsNotSplit)
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">The message.</param>
    public ScoreConflictException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and a cause.</summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The cause.</param>
    public ScoreConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>The conflict.</summary>
    public ScoreConflict Conflict { get; }
}

/// <summary>The audio a score is made from: a channel of a track, or a library item (007 <c>AudioSource</c>).</summary>
/// <param name="TrackId">The track (with <paramref name="Channel"/>).</param>
/// <param name="Channel">A channel id of the track.</param>
/// <param name="AssetId">A library item (alone).</param>
public sealed record ScoreSource(Guid? TrackId, string? Channel, Guid? AssetId)
{
    /// <summary>A channel of a track.</summary>
    /// <param name="trackId">The track.</param>
    /// <param name="channel">The channel id.</param>
    /// <returns>The source.</returns>
    public static ScoreSource FromChannel(Guid trackId, string channel) => new(trackId, channel, null);

    /// <summary>A library item.</summary>
    /// <param name="assetId">The item.</param>
    /// <returns>The source.</returns>
    public static ScoreSource FromAsset(Guid assetId) => new(null, null, assetId);
}

/// <summary>A tempo entered by the user (data-model 4.2); the first downbeat is relative to the source (range) start.</summary>
/// <param name="Bpm">30..300.</param>
/// <param name="BeatsPerBar">1..12.</param>
/// <param name="FirstDownbeatSeconds">0..900.</param>
public sealed record ManualTempo(double Bpm, int BeatsPerBar, double FirstDownbeatSeconds);

/// <summary>The normalised score request with every default resolved, stored with the item (data-model 4.2).</summary>
public sealed record ScoreRecipe
{
    /// <summary>The source.</summary>
    public required ScoreSource Source { get; init; }

    /// <summary>The profile.</summary>
    public required ScoreProfile Profile { get; init; }

    /// <summary>Piano only.</summary>
    public ScoreQuality? Quality { get; init; }

    /// <summary>The quantisation grid.</summary>
    public required ScoreResolution Resolution { get; init; }

    /// <summary>One of <see cref="KeyNames.All"/>, or null for the estimate.</summary>
    public string? KeyOverride { get; init; }

    /// <summary>A manual tempo that overrides the beat grid.</summary>
    public ManualTempo? Tempo { get; init; }

    /// <summary>Part of the source (at least 2 s).</summary>
    public ExportRange? Range { get; init; }

    /// <summary>Tablature settings (feature 016), null without tablature.</summary>
    public TablatureSettings? Tablature { get; init; }

    /// <summary>Part settings (feature 016), null for concert pitch without a clef choice.</summary>
    public PartSettings? Part { get; init; }

    /// <summary>Place chord symbols above the top staff (feature 016).</summary>
    public bool IncludeChords { get; init; }
}

/// <summary>The key of a score.</summary>
/// <param name="Tonic">Tonic in music21 spelling (<c>A-</c> = A-flat).</param>
/// <param name="Mode">Major or minor.</param>
/// <param name="Confidence">0..1.</param>
/// <param name="Source">Estimated or override.</param>
public sealed record KeyInfo(string Tonic, KeyMode Mode, double Confidence, KeySource Source);

/// <summary>A warning stored on a score.</summary>
/// <param name="Code">The code.</param>
/// <param name="Text">English text (1..300 characters).</param>
public sealed record ScoreWarning(ScoreWarningCode Code, string Text);
