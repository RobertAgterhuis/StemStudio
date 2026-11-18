using System.Globalization;

namespace StemStudio.Contracts;

/// <summary>Routes of the instrument views (backend-api 1.15.0, feature 016, tag <c>instrument-views</c>).</summary>
public static partial class ApiRoutes
{
    /// <summary>GET: the static instrument catalogue (<c>getInstrumentCatalogue</c>).</summary>
    public const string InstrumentCatalogue = "/api/instruments/catalogue";

    /// <summary>GET / POST / DELETE the chord chart of a track (<c>getChords</c>, <c>detectChords</c>, <c>deleteChords</c>).</summary>
    /// <param name="trackId">Track id.</param>
    /// <returns>The relative URL.</returns>
    public static string Chords(Guid trackId) => $"{Track(trackId)}/chords";

    /// <summary>PUT: edit one chord segment (<c>editChordSegment</c>).</summary>
    /// <param name="trackId">Track id.</param>
    /// <param name="segmentIndex">0-based segment index.</param>
    /// <returns>The relative URL.</returns>
    public static string ChordSegment(Guid trackId, int segmentIndex) =>
        $"{Chords(trackId)}/segments/{segmentIndex.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>GET: the chord sheet of the lyrics card (<c>getChordSheet</c>).</summary>
    /// <param name="trackId">Track id.</param>
    /// <param name="transpose">012 transpose, -12..12.</param>
    /// <param name="capo">Capo, 0..12.</param>
    /// <returns>The relative URL with the query.</returns>
    public static string ChordSheet(Guid trackId, int transpose, int capo) => $"{Chords(trackId)}/sheet{DisplayQuery(transpose, capo)}";

    /// <summary>GET: the ChordPro download (<c>downloadChordPro</c>).</summary>
    /// <param name="trackId">Track id.</param>
    /// <param name="transpose">012 transpose, -12..12.</param>
    /// <param name="capo">Capo, 0..12.</param>
    /// <returns>The relative URL with the query.</returns>
    public static string ChordPro(Guid trackId, int transpose, int capo) => $"{Chords(trackId)}/chordpro{DisplayQuery(transpose, capo)}";

    /// <summary>GET: capo suggestions (<c>getCapoSuggestions</c>, S12).</summary>
    /// <param name="trackId">Track id.</param>
    /// <param name="transpose">012 transpose, -12..12.</param>
    /// <returns>The relative URL with the query.</returns>
    public static string CapoSuggestions(Guid trackId, int transpose) =>
        $"{Chords(trackId)}/capo-suggestions?transpose={transpose.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>GET: the song key (<c>getKey</c>, S11).</summary>
    /// <param name="trackId">Track id.</param>
    /// <param name="transpose">012 transpose, -12..12.</param>
    /// <param name="instrument">Written instrument id (<c>concert</c> when null).</param>
    /// <returns>The relative URL with the query.</returns>
    public static string Key(Guid trackId, int transpose, string? instrument) =>
        $"{Track(trackId)}/key?transpose={transpose.ToString(CultureInfo.InvariantCulture)}"
        + (instrument is null ? string.Empty : $"&instrument={Uri.EscapeDataString(instrument)}");

    /// <summary>GET / POST / DELETE the drum hit map (<c>getDrumHits</c>, <c>detectDrumHits</c>, <c>deleteDrumHits</c>).</summary>
    /// <param name="trackId">Track id.</param>
    /// <returns>The relative URL.</returns>
    public static string DrumHits(Guid trackId) => $"{Track(trackId)}/drum-hits";

    /// <summary>GET / POST / DELETE the reference melody (<c>getVocalPitch</c>, <c>analyseVocalPitch</c>, <c>deleteVocalPitch</c>).</summary>
    /// <param name="trackId">Track id.</param>
    /// <returns>The relative URL.</returns>
    public static string VocalPitch(Guid trackId) => $"{Track(trackId)}/vocal-pitch";

    private static string DisplayQuery(int transpose, int capo) =>
        string.Create(CultureInfo.InvariantCulture, $"?transpose={transpose}&capo={capo}");
}
