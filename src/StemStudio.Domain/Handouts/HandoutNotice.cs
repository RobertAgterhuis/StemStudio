using StemStudio.Domain.Lyrics;
using StemStudio.Domain.Tracks;

namespace StemStudio.Domain.Handouts;

/// <summary>What a handout song uses, for its attribution lines.</summary>
/// <param name="Lyrics">The lyrics printed, or null.</param>
/// <param name="ChordsUsed">Chords were printed.</param>
/// <param name="Notation">The notation items printed.</param>
/// <param name="Artwork">The artwork printed, or null.</param>
public sealed record AttributionSources(LyricsDocument? Lyrics, bool ChordsUsed, IReadOnlyList<NotationItem> Notation, TrackArtwork? Artwork);

/// <summary>The attribution lines of a handout's last page (data-model 6.6, only those that apply).</summary>
public static class HandoutNotice
{
    /// <summary>Most attribution lines.</summary>
    public const int MaxAttributions = 20;

    /// <summary>Longest attribution line.</summary>
    public const int MaxLength = 300;

    /// <summary>The attribution of automatic chords.</summary>
    public const string ChordsLine = "Chords detected automatically (StemStudio)";

    /// <summary>The attribution lines of all songs, distinct, in order of first use.</summary>
    /// <param name="songs">What each song uses.</param>
    /// <returns>At most 20 lines of at most 300 characters.</returns>
    public static IReadOnlyList<string> Attributions(IEnumerable<AttributionSources> songs)
    {
        ArgumentNullException.ThrowIfNull(songs);
        return songs.SelectMany(Lines).Where(line => !string.IsNullOrWhiteSpace(line)).Select(line => line.Length <= MaxLength ? line : line[..MaxLength])
            .Distinct(StringComparer.Ordinal).Take(MaxAttributions).ToList();
    }

    /// <summary>The source text of an artwork (<c>Artwork: Cover Art Archive</c>).</summary>
    /// <param name="artwork">The artwork.</param>
    /// <returns>The line.</returns>
    public static string ArtworkLine(TrackArtwork artwork)
    {
        ArgumentNullException.ThrowIfNull(artwork);
        return "Artwork: " + artwork.Source switch
        {
            ArtworkSource.User => "your own picture",
            ArtworkSource.Spotify => "Spotify",
            ArtworkSource.Embedded => "embedded in the audio file",
            _ => "Cover Art Archive",
        };
    }

    private static IEnumerable<string> Lines(AttributionSources song)
    {
        if (LyricsCredit.For(song.Lyrics) is { } lyrics)
        {
            yield return lyrics;
        }

        if (song.ChordsUsed)
        {
            yield return ChordsLine;
        }

        foreach (var attribution in song.Notation.Select(item => item.Attribution).OfType<string>())
        {
            yield return attribution;
        }

        if (song.Artwork is { } artwork)
        {
            yield return ArtworkLine(artwork);
        }
    }
}

/// <summary>Names of handout items (data-model 5.4): "&lt;Title&gt; - &lt;Family label&gt; handout", cut to 120 characters.</summary>
public static class HandoutNaming
{
    /// <summary>The contents part of the name (also the provenance contents).</summary>
    /// <param name="familyLabel">The family label.</param>
    /// <returns>"&lt;Family label&gt; handout".</returns>
    public static string Contents(string familyLabel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(familyLabel);
        return $"{familyLabel} handout";
    }
}
