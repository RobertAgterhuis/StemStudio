namespace StemStudio.Contracts.Setlists;

/// <summary>Query options of <c>getSetlistPdf</c> (backend-api 1.13.0, feature 014); every part is included by default.</summary>
/// <param name="Lyrics">Print the lyrics of each song.</param>
/// <param name="Sections">Print the section list of each song.</param>
/// <param name="Notes">Print the setlist and entry notes.</param>
/// <param name="PageBreakPerSong">Start every song on a new page.</param>
public sealed record SetlistPdfOptions(bool Lyrics = true, bool Sections = true, bool Notes = true, bool PageBreakPerSong = true)
{
    /// <summary>Everything included (the Beginner download).</summary>
    public static SetlistPdfOptions Default { get; } = new();
}
