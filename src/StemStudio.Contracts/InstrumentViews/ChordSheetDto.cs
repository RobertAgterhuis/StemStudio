namespace StemStudio.Contracts.InstrumentViews;

/// <summary><c>GET .../chords/sheet</c> (<c>ChordSheet</c>) (backend-api 1.15.0, feature 016).</summary>
public sealed record ChordSheetDto
{
    /// <summary>Transpose (-12..12).</summary>
    public required int Transpose { get; init; }

    /// <summary>Capo (0..12).</summary>
    public required int Capo { get; init; }

    /// <summary>Rows (at most 2000).</summary>
    public required IReadOnlyList<ChordSheetRowDto> Rows { get; init; }
}
