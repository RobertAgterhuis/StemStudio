namespace StemStudio.Domain.Handouts;

/// <summary>The seven equal handout families (data-model 6.1).</summary>
public enum HandoutFamily
{
    /// <summary>Band overview.</summary>
    Overview,

    /// <summary>Vocals.</summary>
    Vocals,

    /// <summary>Guitar.</summary>
    Guitar,

    /// <summary>Bass.</summary>
    Bass,

    /// <summary>Keys.</summary>
    Keys,

    /// <summary>Drums.</summary>
    Drums,

    /// <summary>Melodic instruments (C, B-flat, E-flat, F).</summary>
    Melodic,
}

/// <summary>Song or setlist handout.</summary>
public enum HandoutKind
{
    /// <summary>One song.</summary>
    Song,

    /// <summary>A setlist.</summary>
    Setlist,
}

/// <summary>Transposition of a melodic handout.</summary>
public enum Transposition
{
    /// <summary>Concert pitch.</summary>
    C,

    /// <summary>B-flat instruments.</summary>
    BFlat,

    /// <summary>E-flat instruments.</summary>
    EFlat,

    /// <summary>F instruments.</summary>
    F,
}

/// <summary>Paper size.</summary>
public enum Paper
{
    /// <summary>A4.</summary>
    A4,

    /// <summary>US Letter.</summary>
    Letter,
}

/// <summary>Handout ingredients (data-model 6.2).</summary>
public enum IngredientCode
{
    /// <summary>Tempo, metre, key, duration, artwork.</summary>
    Facts,

    /// <summary>Sections, bars, practice tempo.</summary>
    FormChart,

    /// <summary>Chords over the lyrics.</summary>
    ChordsOverLyrics,

    /// <summary>Plain lyrics (cues for drums).</summary>
    Lyrics,

    /// <summary>Chords per section and bar.</summary>
    ChordChart,

    /// <summary>Score, tab, drum notation or transposed part.</summary>
    Notation,

    /// <summary>The family's practice notes.</summary>
    PracticeNotes,

    /// <summary>Tempo reached per section (014 log).</summary>
    PracticeProgress,

    /// <summary>Setlist and entry notes (setlist handouts only).</summary>
    SetlistNotes,
}

/// <summary>Why an ingredient was left out.</summary>
public enum OmissionReason
{
    /// <summary>The song has no data for it.</summary>
    NotAvailable,

    /// <summary>Not chosen.</summary>
    NotSelected,

    /// <summary>The song has no timing (beat grid) for it.</summary>
    NoTiming,
}

/// <summary>Renderer warning codes (worker <c>HandoutWarningCode</c>).</summary>
public enum HandoutWarningCode
{
    /// <summary>Characters no font has.</summary>
    MissingGlyphs,

    /// <summary>A notation page was scaled to the paper.</summary>
    NotationScaled,

    /// <summary>A notation file could not be read.</summary>
    NotationUnreadable,
}

/// <summary>Kind of a notation item in a handout.</summary>
public enum NotationKind
{
    /// <summary>Standard score.</summary>
    Score,

    /// <summary>Tablature.</summary>
    Tab,

    /// <summary>Drum notation.</summary>
    Drums,

    /// <summary>Transposed part.</summary>
    Transposed,
}

/// <summary>A renderer warning.</summary>
/// <param name="Code">Code.</param>
/// <param name="Text">Text (at most 300 characters).</param>
public sealed record HandoutWarning(HandoutWarningCode Code, string Text);

/// <summary>An ingredient left out, with its reason.</summary>
/// <param name="Code">Ingredient.</param>
/// <param name="Reason">Reason.</param>
public sealed record OmittedIngredient(IngredientCode Code, OmissionReason Reason);

/// <summary>
/// <c>Provenance.HandoutRecipe</c> (data-model 5.4): what was asked; "Make again" posts it unchanged, so the data is read again.
/// </summary>
public sealed record HandoutRecipe
{
    /// <summary>Song or setlist.</summary>
    public required HandoutKind Kind { get; init; }

    /// <summary>The song; null for a setlist handout.</summary>
    public Guid? TrackId { get; init; }

    /// <summary>The setlist; null for a song handout.</summary>
    public Guid? SetlistId { get; init; }

    /// <summary>Setlist name at creation; null for a song handout.</summary>
    public string? SetlistName { get; init; }

    /// <summary>Family.</summary>
    public required HandoutFamily Family { get; init; }

    /// <summary>Ingredients asked for; null = the family defaults.</summary>
    public IReadOnlyList<IngredientCode>? Include { get; init; }

    /// <summary>Notation items asked for; null = the default notation.</summary>
    public IReadOnlyList<Guid>? NotationAssetIds { get; init; }

    /// <summary>Transposition (melodic only); null otherwise.</summary>
    public Transposition? Transposition { get; init; }

    /// <summary>Paper.</summary>
    public required Paper Paper { get; init; }
}

/// <summary>
/// <c>Asset.Handout</c> (data-model 5.4): the plan of a handout item, set at creation; pages, warnings and outline are filled
/// when the worker finished it (shown on the wire only once the item is ready).
/// </summary>
public sealed record HandoutFacts
{
    /// <summary>Song or setlist.</summary>
    public required HandoutKind Kind { get; init; }

    /// <summary>Family.</summary>
    public required HandoutFamily Family { get; init; }

    /// <summary>Transposition; null when none.</summary>
    public Transposition? Transposition { get; init; }

    /// <summary>Paper.</summary>
    public required Paper Paper { get; init; }

    /// <summary>Page count; 0 until ready.</summary>
    public int Pages { get; init; }

    /// <summary>Number of songs.</summary>
    public required int Songs { get; init; }

    /// <summary>Ingredients in the PDF.</summary>
    public IReadOnlyList<IngredientCode> Included { get; init; } = [];

    /// <summary>Ingredients left out.</summary>
    public IReadOnlyList<OmittedIngredient> Omitted { get; init; } = [];

    /// <summary>Renderer warnings.</summary>
    public IReadOnlyList<HandoutWarning> Warnings { get; init; } = [];

    /// <summary>Bookmark titles in order.</summary>
    public IReadOnlyList<string> Outline { get; init; } = [];
}
