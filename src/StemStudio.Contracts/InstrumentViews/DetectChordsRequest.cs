namespace StemStudio.Contracts.InstrumentViews;

/// <summary>Optional body of <c>POST .../chords</c> (<c>DetectChordsRequest</c>) (backend-api 1.15.0, feature 016).</summary>
public sealed record DetectChordsRequest
{
    /// <summary>Vocabulary (default simple).</summary>
    public ChordVocabularyDto Vocabulary { get; init; } = ChordVocabularyDto.Simple;

    /// <summary>Engine (Expert); null = the worker default.</summary>
    public ChordEngineDto? Engine { get; init; }

    /// <summary>Replace a chart with edits (else 409 <c>edits-exist</c>).</summary>
    public bool OverwriteEdits { get; init; }
}
