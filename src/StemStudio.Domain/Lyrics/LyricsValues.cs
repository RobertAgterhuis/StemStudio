namespace StemStudio.Domain.Lyrics;

/// <summary>Lyrics state of a track (feature 006, data-model 3.2).</summary>
public enum LyricsStatus
{
    /// <summary>Never looked up (older tracks, or before the duration is known).</summary>
    None = 0,

    /// <summary>The LRCLIB lookup is scheduled or running (no job yet = to be submitted).</summary>
    Searching = 1,

    /// <summary>Timed lyrics are available.</summary>
    Found = 2,

    /// <summary>The source marks the song as instrumental.</summary>
    Instrumental = 3,

    /// <summary>No lyrics were found online.</summary>
    NotFound = 4,

    /// <summary>Text without timing; the alignment has not started.</summary>
    AlignPending = 5,

    /// <summary>The text is being aligned (no job yet = to be submitted).</summary>
    Aligning = 6,

    /// <summary>Lookup or alignment failed; see the reason.</summary>
    Failed = 7,

    /// <summary>Online lookups are switched off in the worker.</summary>
    Disabled = 8,

    /// <summary>Removed by the user; no automatic lookup any more.</summary>
    Removed = 9,
}

/// <summary>Best timing of a lyrics document.</summary>
public enum SyncLevel
{
    /// <summary>Word timing.</summary>
    Word,

    /// <summary>Line timing; word times are interpolated.</summary>
    Line,

    /// <summary>Text only.</summary>
    Plain,
}

/// <summary>Dominant word origin of a document.</summary>
public enum LyricsOrigin
{
    /// <summary>From the lyrics source.</summary>
    Source,

    /// <summary>Estimated from the line timing.</summary>
    Interpolated,

    /// <summary>From the automatic alignment.</summary>
    Aligned,

    /// <summary>No timing (plain document).</summary>
    None,
}

/// <summary>Where a word time comes from.</summary>
public enum WordOrigin
{
    /// <summary>From the lyrics source.</summary>
    Source,

    /// <summary>Estimated from the line timing.</summary>
    Interpolated,

    /// <summary>From the automatic alignment.</summary>
    Aligned,
}

/// <summary>Where the lyrics text comes from.</summary>
public enum LyricsSource
{
    /// <summary>LRCLIB.</summary>
    Lrclib,

    /// <summary>Entered by the user.</summary>
    User,
}

/// <summary>Outcome of a completed lyrics lookup.</summary>
public enum LyricsOutcome
{
    /// <summary>Lyrics were found (word, line or plain).</summary>
    Found,

    /// <summary>The song is instrumental.</summary>
    Instrumental,

    /// <summary>Nothing was found.</summary>
    NotFound,

    /// <summary>Online lookups are switched off.</summary>
    Disabled,
}

/// <summary>When the worker's automatic alignment applies (<c>STEMSTUDIO_ALIGN_AUTO</c>).</summary>
public enum AlignAuto
{
    /// <summary>Only on a GPU worker (default).</summary>
    Gpu,

    /// <summary>Always.</summary>
    Always,

    /// <summary>Never; the user clicks Align.</summary>
    Never,
}

/// <summary>Why a lyrics operation is not allowed in the current state.</summary>
public enum LyricsConflict
{
    /// <summary>A search or an alignment is running.</summary>
    Busy,

    /// <summary>There is no text to align.</summary>
    NoText,

    /// <summary>The lyrics already have word timing.</summary>
    AlreadyTimed,

    /// <summary>There are no lyrics (e.g. to offset).</summary>
    None,

    /// <summary>No alignment is running.</summary>
    NotAligning,
}

/// <summary>A lyrics operation is not allowed in the current state (the API answers 409).</summary>
public sealed class LyricsConflictException : InvalidOperationException
{
    /// <summary>Creates the exception for a conflict.</summary>
    /// <param name="conflict">The conflict.</param>
    public LyricsConflictException(LyricsConflict conflict)
        : base($"The lyrics operation conflicts with the current state: {conflict}.")
    {
        Conflict = conflict;
    }

    /// <summary>Creates a <see cref="LyricsConflict.Busy"/> exception.</summary>
    public LyricsConflictException()
        : this(LyricsConflict.Busy)
    {
    }

    /// <summary>Creates a <see cref="LyricsConflict.Busy"/> exception with a message.</summary>
    /// <param name="message">The message.</param>
    public LyricsConflictException(string message)
        : base(message)
    {
    }

    /// <summary>Creates a <see cref="LyricsConflict.Busy"/> exception with a message and a cause.</summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The cause.</param>
    public LyricsConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>The conflict.</summary>
    public LyricsConflict Conflict { get; }
}
