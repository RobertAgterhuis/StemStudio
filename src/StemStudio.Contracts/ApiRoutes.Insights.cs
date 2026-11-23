using System.Text.Json;
using StemStudio.Contracts.Handouts;

namespace StemStudio.Contracts;

/// <summary>Routes of the stem quality report, handouts and practice notes (backend-api 1.19.0, feature 020, tags <c>insights</c> and <c>handouts</c>).</summary>
public static partial class ApiRoutes
{
    /// <summary>POST: create a handout library item (<c>createHandout</c>).</summary>
    public const string Handouts = "/api/assets/handouts";

    /// <summary>GET / POST the stem quality report of a track (<c>getQualityReport</c>, <c>requestQualityReport</c>).</summary>
    /// <param name="trackId">Track id.</param>
    /// <returns>The relative URL.</returns>
    public static string Quality(Guid trackId) => $"{Track(trackId)}/quality";

    /// <summary>GET: handout options for a song or a setlist (<c>getHandoutOptions</c>).</summary>
    /// <param name="source">Song or setlist.</param>
    /// <param name="family">Family.</param>
    /// <param name="transposition">Transposition (<c>melodic</c> only); null = none in the query.</param>
    /// <returns>The relative URL with the query.</returns>
    public static string HandoutOptions(HandoutSourceDto source, HandoutFamilyDto family, TranspositionDto? transposition = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        var subject = source.TrackId is { } trackId ? $"trackId={trackId}" : $"setlistId={source.SetlistId}";
        var query = $"/api/handouts/options?{subject}&family={WireValue(family)}";
        return transposition is { } value ? $"{query}&transposition={WireValue(value)}" : query;
    }

    /// <summary>GET: the practice notes of a track (<c>getPracticeNotes</c>).</summary>
    /// <param name="trackId">Track id.</param>
    /// <returns>The relative URL.</returns>
    public static string PracticeNotes(Guid trackId) => $"{Track(trackId)}/practice-notes";

    /// <summary>PUT: the practice note of one family (<c>setPracticeNote</c>).</summary>
    /// <param name="trackId">Track id.</param>
    /// <param name="family">Family.</param>
    /// <returns>The relative URL.</returns>
    public static string PracticeNote(Guid trackId, HandoutFamilyDto family) => $"{PracticeNotes(trackId)}/{WireValue(family)}";

    private static string WireValue<TEnum>(TEnum value)
        where TEnum : struct, Enum => JsonSerializer.Serialize(value).Trim('"');
}
