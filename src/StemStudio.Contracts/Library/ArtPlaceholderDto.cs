namespace StemStudio.Contracts.Library;

/// <summary>The lettered placeholder of a folder (backend-api 1.22.0 <c>ArtPlaceholder</c>), always given as image fallback.</summary>
public sealed record ArtPlaceholderDto
{
    /// <summary>One grapheme: the first letter (upper case where the script has case), <c>?</c> for unknown folders, a note sign for "My songs".</summary>
    public required string Glyph { get; init; }

    /// <summary>Index of the design-system art colour token, 0 to <see cref="ContractLimits.ArtToneCount"/> - 1, derived from the folder key.</summary>
    public required int Tone { get; init; }
}
